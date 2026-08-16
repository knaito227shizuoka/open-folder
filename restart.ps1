# Rebuild and restart open-folder.
# The running publish build blocks a second instance via Mutex, so stop it first.
$ErrorActionPreference = 'Stop'

Get-Process OpenFolder -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500  # wait for the exe lock to be released

dotnet publish -c Release -o (Join-Path $PSScriptRoot 'publish\OpenFolder') --nologo $PSScriptRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Start-Process (Join-Path $PSScriptRoot 'publish\OpenFolder\OpenFolder.exe')
Write-Host 'Restarted.'
