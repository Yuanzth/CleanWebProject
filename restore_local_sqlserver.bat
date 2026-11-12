@echo off
echo ========================================================
echo   RESTORE DATABASE KE SQL SERVER LOKAL LAPTOP
echo ========================================================
echo.
echo Pastikan service SQL Server (MSSQLSERVER) sudah berjalan!
echo (Jika belum, jalankan di PowerShell Admin: net start MSSQLSERVER)
echo.

set SERVER=localhost
set /p SERVER="Masukkan Host/Server SQL Server [localhost]: "
if "%SERVER%"=="" set SERVER=localhost

set AUTH=Y
set /p AUTH="Gunakan Windows Authentication? (Y/N) [Y]: "
if "%AUTH%"=="" set AUTH=Y

if /I "%AUTH%"=="Y" (
    set SQLCMD_AUTH=-E
) else (
    set USER=sa
    set /p USER="Masukkan Username [sa]: "
    if "%USER%"=="" set USER=sa
    set /p PASS="Masukkan Password: "
    set SQLCMD_AUTH=-U "%USER%" -P "%PASS%"
)

echo.
echo [1/4] Membuat database db_infrahub dan db_itassettrack...
sqlcmd -S "%SERVER%" %SQLCMD_AUTH% -i "database\init-scripts\01_init_databases.sql"

echo.
echo [2/4] Me-restore skema dan master data db_itassettrack...
sqlcmd -S "%SERVER%" %SQLCMD_AUTH% -d db_itassettrack -i "database\init-scripts\02_restore_itassettrack.sql"

echo.
echo [3/4] Me-restore skema dan master data db_infrahub...
sqlcmd -S "%SERVER%" %SQLCMD_AUTH% -d db_infrahub -i "database\init-scripts\03_restore_monitoring_master.sql"

echo.
echo [4/4] Membuat Stored Procedures monitoring hub...
sqlcmd -S "%SERVER%" %SQLCMD_AUTH% -d db_infrahub -i "database\init-scripts\04_create_procedures_monitoring.sql"

echo.
echo ========================================================
echo Restore Database Lokal Selesai!
echo ========================================================
pause
