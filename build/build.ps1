# Builds Cloudbreak and its installer.
#   powershell -ExecutionPolicy Bypass -File build\build.ps1
# Output: Setup\Cloudbreak-Setup-<version>.exe  (temporary build folders are removed at the end)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup' }

if (-not (Test-Path "$root\assets\cloudbreak.ico")) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\make-icon.ps1"
}

$publish = "$root\publish"
Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue
& $dotnet publish "$root\src\Cloudbreak\Cloudbreak.csproj" -c Release -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Remove-Item "$root\Setup" -Recurse -Force -ErrorAction SilentlyContinue
& $iscc /Q "$PSScriptRoot\Cloudbreak.iss"
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }

# Leave only the installer behind
Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue
& $dotnet build-server shutdown *> $null

Get-ChildItem "$root\Setup\*.exe" | ForEach-Object { "{0}  ({1:N1} MB)" -f $_.FullName, ($_.Length / 1MB) }
