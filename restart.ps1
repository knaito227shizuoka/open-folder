# open-folder を再ビルドして再起動する。
# 常駐中の publish 版は Mutex で多重起動できないため、先に終了させる。
$ErrorActionPreference = 'Stop'

Get-Process OpenFolder -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500  # exe のロック解放を待つ

dotnet publish -c Release -o (Join-Path $PSScriptRoot 'publish\OpenFolder') --nologo $PSScriptRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Start-Process (Join-Path $PSScriptRoot 'publish\OpenFolder\OpenFolder.exe')
Write-Host '再起動しました。'
