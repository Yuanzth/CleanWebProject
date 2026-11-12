# Build Script: PyInstaller Onedir for ITAssetTrack Agent with Auto Code Signing
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Building ITAssetTrack Agent (PyInstaller Onedir + Code Signing)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Pastikan file icon dan version info ada
if (-not (Test-Path "$scriptDir\itassettrack.ico")) {
    Copy-Item "d:\webserver\www\pamasmo\Services\itassettrack.ico" "$scriptDir\itassettrack.ico" -Force
}

# 2. Jalankan PyInstaller
Write-Host "[1/4] Menjalankan PyInstaller..." -ForegroundColor Yellow
python -m PyInstaller --noconfirm --clean "$scriptDir\agent_service.spec"

# 3. Salin hasil build ke build_method_2_pyinstaller_onedir
$targetDir = "$scriptDir\build_method_2_pyinstaller_onedir\itassettrack_pulse"
Write-Host "[2/4] Menyalin output ke $targetDir..." -ForegroundColor Yellow

if (Test-Path $targetDir) {
    Remove-Item -Path $targetDir -Recurse -Force
}
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
Copy-Item -Path "$scriptDir\dist\itassettrack_pulse\*" -Destination $targetDir -Recurse -Force

# Pastikan itassettrack.ico ada di dalam folder target
Copy-Item -Path "$scriptDir\itassettrack.ico" -Destination "$targetDir\itassettrack.ico" -Force

# 4. Code Signing Executable
$targetExe = "$targetDir\itassettrack_pulse.exe"
if (Test-Path $targetExe) {
    Write-Host "[3/4] Menandatangani (Code Signing) Binary $targetExe..." -ForegroundColor Yellow
    & "$scriptDir\create_certificate_and_sign.ps1" -TargetExe $targetExe
} else {
    Write-Error "File $targetExe tidak ditemukan setelah build!"
}

# 5. Verifikasi Akhir
Write-Host "[4/4] Verifikasi Akhir Metadata & Signature..." -ForegroundColor Green
$v = (Get-Item $targetExe).VersionInfo
$sig = Get-AuthenticodeSignature $targetExe

Write-Host "----------------------------------------------------------" -ForegroundColor Gray
Write-Host "File Name        : $($v.FileName)" -ForegroundColor White
Write-Host "File Description : $($v.FileDescription)" -ForegroundColor Green
Write-Host "Product Name     : $($v.ProductName)" -ForegroundColor Green
Write-Host "File Version     : $($v.FileVersion)" -ForegroundColor Green
Write-Host "Product Version  : $($v.ProductVersion)" -ForegroundColor Green
Write-Host "Digital Signer   : $($sig.SignerCertificate.Subject)" -ForegroundColor Green
Write-Host "Signature Status : $($sig.Status)" -ForegroundColor Green
Write-Host "----------------------------------------------------------" -ForegroundColor Gray
Write-Host "[+] Build dan Code Signing Berhasil!" -ForegroundColor Green
