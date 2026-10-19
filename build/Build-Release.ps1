[CmdletBinding()]
param(
    [string]$Version = '1.0.0',
    [switch]$SkipDriverPayload
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root 'src\AudioBridge\AudioBridge.csproj'
$InstallerProject = Join-Path $Root 'installer\AudioBridge.Installer.wixproj'
$DriverAssets = Join-Path $Root 'src\AudioBridge\Assets'
$DriverZip = Join-Path $DriverAssets 'VBCABLE_Driver_Pack45.zip'
$PublishDir = Join-Path $Root 'artifacts\publish'
$ArtifactDir = Join-Path $Root 'artifacts'
$DriverUrl = 'https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip'
$TempDownloadedDriver = $false

if ($Version.StartsWith('v')) { $Version = $Version.Substring(1) }
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be a numeric version such as 1.0.0. Received '$Version'."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET 10 SDK is required only for building the release. End users do not need the SDK; they install the MSI.'
}

New-Item -ItemType Directory -Force -Path $ArtifactDir, $PublishDir, $DriverAssets | Out-Null
if (-not $SkipDriverPayload -and -not (Test-Path $DriverZip)) {
    Write-Host 'Downloading the official VB-Audio VB-CABLE package for embedding in this release...' -ForegroundColor Cyan
    Invoke-WebRequest -Uri $DriverUrl -OutFile $DriverZip
    $TempDownloadedDriver = $true
}
if (-not $SkipDriverPayload) {
    if (-not (Test-Path $DriverZip)) { throw 'VB-CABLE payload is missing.' }
    $length = (Get-Item $DriverZip).Length
    if ($length -lt 100000 -or $length -gt 25MB) { throw "Unexpected VB-CABLE package size ($length bytes). Aborting the build." }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($DriverZip)
    try {
        if (-not ($archive.Entries | Where-Object { $_.FullName -match '(?i)VBCABLE_Setup_x64\.exe$' })) {
            throw 'The official VB-CABLE package did not contain the expected 64-bit setup program. Check its contents before building.'
        }
    } finally { $archive.Dispose() }
}

try {
    Write-Host "Publishing AudioBridge $Version as self-contained Windows x64 single-file EXE..." -ForegroundColor Cyan
    Remove-Item $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
    & dotnet publish $Project --configuration Release --runtime win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
        -p:Version=$Version --output $PublishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path (Join-Path $PublishDir 'AudioBridge.exe'))) { throw 'AudioBridge.exe was not produced.' }

    Write-Host 'Building the MSI installer...' -ForegroundColor Cyan
    & dotnet build $InstallerProject --configuration Release `
        -p:AppPublishDir="$PublishDir\" -p:ProductVersion=$Version
    if ($LASTEXITCODE -ne 0) { throw "MSI build failed with exit code $LASTEXITCODE." }

    $BuiltMsi = Get-ChildItem (Join-Path $Root 'installer\bin\Release') -Filter 'AudioBridge-Setup.msi' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $BuiltMsi) { throw 'The build succeeded but AudioBridge-Setup.msi was not found under installer\bin\Release.' }
    Copy-Item $BuiltMsi.FullName (Join-Path $ArtifactDir 'AudioBridge-Setup.msi') -Force
    Copy-Item (Join-Path $PublishDir 'AudioBridge.exe') (Join-Path $ArtifactDir 'AudioBridge.exe') -Force
    Get-FileHash (Join-Path $ArtifactDir 'AudioBridge-Setup.msi') -Algorithm SHA256 | Format-List | Out-File (Join-Path $ArtifactDir 'SHA256.txt')
    Get-FileHash (Join-Path $ArtifactDir 'AudioBridge.exe') -Algorithm SHA256 | Format-List | Out-File -Append (Join-Path $ArtifactDir 'SHA256.txt')

    Write-Host ''
    Write-Host 'Release build completed:' -ForegroundColor Green
    Write-Host (Join-Path $ArtifactDir 'AudioBridge-Setup.msi') -ForegroundColor Green
    Write-Host (Join-Path $ArtifactDir 'AudioBridge.exe') -ForegroundColor Green
    Write-Host 'The release binary contains the official VB-CABLE driver package unless -SkipDriverPayload was used.' -ForegroundColor Gray
}
finally {
    if ($TempDownloadedDriver -and (Test-Path $DriverZip)) {
        Remove-Item $DriverZip -Force -ErrorAction SilentlyContinue
    }
}
