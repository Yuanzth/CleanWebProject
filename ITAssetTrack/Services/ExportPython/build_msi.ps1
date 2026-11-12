# Build Script: Compile Native WiX v5 MSI Package & Sign with Authenticode
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Building ITAssetTrack Agent Native Windows MSI Package (WiX)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Pastikan folder output installer ada
$distDir = "$scriptDir\dist_installer"
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

$msiFile = "$distDir\ITAssetTrack_Pulse_v1.2.0_x64.msi"
if (Test-Path $msiFile) {
    Remove-Item -Path $msiFile -Force
}

# 2. Compile MSI menggunakan WiX Toolset v5
Write-Host "[1/3] Mengompilasi paket MSI dengan WiX v5..." -ForegroundColor Yellow
wix build -arch x64 -out $msiFile "$scriptDir\itassettrack_pulse.wxs"

if (-not (Test-Path $msiFile)) {
    Write-Error "Gagal membuat file MSI: $msiFile"
}

# 3. Sign file MSI menggunakan Code Signing Certificate PT InfraHub Enterprise
Write-Host "[2/3] Menandatangani (Code Signing) Paket MSI..." -ForegroundColor Yellow
& "$scriptDir\create_certificate_and_sign.ps1" -TargetExe $msiFile

# 4. Verifikasi Akhir
Write-Host "[3/3] Memeriksa Metadata & Signature Paket MSI..." -ForegroundColor Green
$item = Get-Item $msiFile
$sig = Get-AuthenticodeSignature $msiFile

Write-Host "----------------------------------------------------------" -ForegroundColor Gray
Write-Host "MSI File Path    : $($item.FullName)" -ForegroundColor White
Write-Host "MSI File Size    : $([math]::Round($item.Length / 1MB, 2)) MB" -ForegroundColor White
Write-Host "Digital Signer   : $($sig.SignerCertificate.Subject)" -ForegroundColor Green
Write-Host "Signature Status : $($sig.Status)" -ForegroundColor Green
Write-Host "Timestamp        : $($sig.TimeStamperCertificate.Subject)" -ForegroundColor Green
Write-Host "----------------------------------------------------------" -ForegroundColor Gray
Write-Host "[+] Paket MSI Berhasil Dibuat dan Ditandatangani!" -ForegroundColor Green
