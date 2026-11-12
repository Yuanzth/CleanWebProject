# Enterprise Installer & Deployment Script: ITAssetTrack Agent
param (
    [switch]$Uninstall = $false
)

$ErrorActionPreference = "Stop"

$installDir = "C:\Program Files\ITAssetTrack Agent"
$dataDir = "C:\ProgramData\EendsoPulse"
$regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$regName = "ITAssetTrack_Pulse"
$exePath = "$installDir\itassettrack_pulse.exe"

# Cek Administrator Privilege
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warning "Skrip ini memerlukan hak Administrator untuk memasang ke Program Files."
}

if ($Uninstall) {
    Write-Host "==========================================================" -ForegroundColor Yellow
    Write-Host "  Uninstalling ITAssetTrack Agent..." -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Yellow
    
    # 1. Hentikan proses yang sedang berjalan
    Stop-Process -Name "itassettrack_pulse" -Force -ErrorAction SilentlyContinue
    
    # 2. Hapus Autostart Registry
    Remove-ItemProperty -Path $regPath -Name $regName -ErrorAction SilentlyContinue
    
    # 3. Hapus Program Files
    if (Test-Path $installDir) {
        Remove-Item -Path $installDir -Recurse -Force
    }
    
    Write-Host "[+] ITAssetTrack Agent berhasil di-uninstall." -ForegroundColor Green
    exit 0
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Installing ITAssetTrack Agent (Enterprise Setup)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = "$scriptDir\build_method_2_pyinstaller_onedir\itassettrack_pulse"

if (-not (Test-Path "$sourceDir\itassettrack_pulse.exe")) {
    Write-Error "Binary $sourceDir\itassettrack_pulse.exe belum di-build! Jalankan build_onedir.ps1 terlebih dahulu."
}

# 1. Hentikan proses lama jika ada
Stop-Process -Name "itassettrack_pulse" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# 2. Siapkan Folder Program Files
Write-Host "[1/4] Menyiapkan Direktori Program Files: $installDir..." -ForegroundColor Yellow
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}
Copy-Item -Path "$sourceDir\*" -Destination $installDir -Recurse -Force

# 3. Siapkan Folder ProgramData untuk DB & Log Lokal
Write-Host "[2/4] Menyiapkan Direktori Data: $dataDir..." -ForegroundColor Yellow
if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
}

# Terapkan pengamanan ACL ketat pada folder ProgramData (Hanya SYSTEM & Admin yang Full Control)
try {
    icacls $dataDir /grant:r "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" "Users:(OI)(CI)RX" /t /c | Out-Null
} catch {
    Write-Warning "Gagal mengatur ACL pada $dataDir ($e)"
}

# 4. Daftarkan Autostart Windows (HKLM Run)
Write-Host "[3/4] Mendaftarkan Startup Registry ($regName)..." -ForegroundColor Yellow
Set-ItemProperty -Path $regPath -Name $regName -Value "`"$exePath`"" -Force

# 5. Jalankan Aplikasi
Write-Host "[4/4] Memulai ITAssetTrack Agent..." -ForegroundColor Yellow
Start-Process -FilePath $exePath

Write-Host "----------------------------------------------------------" -ForegroundColor Gray
Write-Host "[+] Instalasi Selesai! ITAssetTrack Agent aktif di System Tray." -ForegroundColor Green
Write-Host "----------------------------------------------------------" -ForegroundColor Gray
