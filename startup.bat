@echo off
rem Create a shortcut to OpenFolder.exe in the user's Startup folder (auto-run on logon).

set "TARGET=%~dp0publish\OpenFolder\OpenFolder.exe"

if not exist "%TARGET%" (
    echo OpenFolder.exe not found: %TARGET%
    echo Run .\restart.bat or dotnet publish first.
    exit /b 1
)

powershell -NoProfile -Command "$ws = New-Object -ComObject WScript.Shell; $startup = $ws.SpecialFolders('Startup'); $lnk = $ws.CreateShortcut((Join-Path $startup 'OpenFolder.lnk')); $lnk.TargetPath = $env:TARGET; $lnk.WorkingDirectory = (Split-Path $env:TARGET); $lnk.Save(); Write-Host ('Created: ' + (Join-Path $startup 'OpenFolder.lnk'))"
