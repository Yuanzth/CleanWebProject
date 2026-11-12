# Script: Create Self-Signed Enterprise Code Signing Certificate & Sign Binary
param (
    [string]$TargetExe = "d:\webserver\www\pamasmo\Services\ExportPython\build_method_2_pyinstaller_onedir\itassettrack_pulse\itassettrack_pulse.exe"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Authenticode Code Signing - PT InfraHub Enterprise" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Cari atau buat Sertifikat Code Signing Perusahaan
$certSubject = "CN=PT InfraHub Enterprise - ITAssetTrack Agent, O=PT InfraHub Enterprise, OU=CIS INFRA_DEPT"
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -like "*ITAssetTrack Agent*" } | Select-Object -First 1

if (-not $cert) {
    Write-Host "[1/3] Membuat Sertifikat Code Signing Baru..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $certSubject `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(5)
    Write-Host "[+] Sertifikat berhasil dibuat: $($cert.Thumbprint)" -ForegroundColor Green
} else {
    Write-Host "[1/3] Menggunakan Sertifikat yang sudah ada: $($cert.Thumbprint)" -ForegroundColor Green
}

# 2. Periksa apakah target file ada
if (-not (Test-Path $TargetExe)) {
    Write-Error "File target $TargetExe tidak ditemukan!"
}

# 3. Sign Executable dengan Authenticode SHA-256 & RFC 3161 Timestamp
Write-Host "[2/3] Menandatangani (Sign) file $TargetExe..." -ForegroundColor Yellow
$signResult = Set-AuthenticodeSignature `
    -FilePath $TargetExe `
    -Certificate $cert `
    -TimestampServer "http://timestamp.digicert.com" `
    -HashAlgorithm SHA256

# 4. Verifikasi Hasil Signature
Write-Host "[3/3] Memeriksa Status Digital Signature..." -ForegroundColor Yellow
$verify = Get-AuthenticodeSignature $TargetExe

Write-Host "----------------------------------------------------------" -ForegroundColor Gray
Write-Host "File Name     : $(Split-Path $TargetExe -Leaf)" -ForegroundColor White
Write-Host "Signer        : $($verify.SignerCertificate.Subject)" -ForegroundColor Green
Write-Host "Status        : $($verify.Status)" -ForegroundColor Green
Write-Host "StatusMessage : $($verify.StatusMessage)" -ForegroundColor White
Write-Host "Thumbprint    : $($verify.SignerCertificate.Thumbprint)" -ForegroundColor White
Write-Host "Timestamp     : $($verify.TimeStamperCertificate.Subject)" -ForegroundColor White
Write-Host "----------------------------------------------------------" -ForegroundColor Gray
Write-Host "[+] Digital Signing Selesai! File siap diupload ke VirusTotal." -ForegroundColor Green
