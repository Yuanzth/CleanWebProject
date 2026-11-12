@echo off
echo ========================================================
echo   InfraHub INTERNSHIP: MENJALANKAN APLIKASI .NET LOKAL
echo ========================================================
echo.
echo Meluncurkan InfraHub di http://localhost:5000 ...
start "InfraHub" cmd /k "cd /d "%~dp0InfraHub.Web" && dotnet run --project InfraHub.Web.csproj --urls="http://localhost:5000""

echo Meluncurkan ITAssetTrack di http://localhost:5001 ...
start "ITAssetTrack" cmd /k "cd /d "%~dp0ITAssetTrack\ITAssetTrack.Web" && dotnet run --urls="http://localhost:5001""

echo.
echo ========================================================
echo Kedua aplikasi .NET sedang berjalan di jendela terpisah!
echo.
echo 1. InfraHub : http://localhost:5000
echo    Login               : admin_infrahub / AdminPass123!
echo.
echo 2. ITAssetTrack    : http://localhost:5001
echo    Login               : admin / Password123!
echo ========================================================
pause
