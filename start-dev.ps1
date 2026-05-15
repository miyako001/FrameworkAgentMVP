param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendPath = Join-Path $root "backend\FrameAgentWordFill"
$frontendPath = Join-Path $root "frontend"

if (-not (Test-Path $backendPath)) {
    throw "Backend path not found: $backendPath"
}

if (-not (Test-Path $frontendPath)) {
    throw "Frontend path not found: $frontendPath"
}

$backendCommand = "Set-Location '$backendPath'; dotnet run"
$frontendCommand = "Set-Location '$frontendPath'; npm run dev"

if ($DryRun) {
    Write-Host "[DryRun] Backend command: $backendCommand"
    Write-Host "[DryRun] Frontend command: $frontendCommand"
    exit 0
}

Write-Host "Starting backend..." -ForegroundColor Green
Start-Process -FilePath "powershell.exe" -ArgumentList @(
    "-NoExit",
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-Command", "$Host.UI.RawUI.WindowTitle='FrameworkAgent Backend'; $backendCommand"
)

Write-Host "Starting frontend..." -ForegroundColor Green
Start-Process -FilePath "powershell.exe" -ArgumentList @(
    "-NoExit",
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-Command", "$Host.UI.RawUI.WindowTitle='FrameworkAgent Frontend'; $frontendCommand"
)

Write-Host "Done. Backend and frontend have been launched in separate terminal windows." -ForegroundColor Cyan
