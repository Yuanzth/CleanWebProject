@echo off
echo ========================================================
echo   InfraHub INTERNSHIP: MENJALANKAN DOCKER DATABASE & GENERATOR
echo ========================================================
echo.
echo Memastikan Docker Desktop aktif dan menjalankan docker-compose...
docker compose up -d --build
echo.
echo ========================================================
echo Container berhasil diluncurkan!
echo - SQL Server 2022 : localhost:1433
echo - db-init         : Otomatis me-restore database dan stored procedures
echo - data-generator  : Otomatis mengisi data dummy setiap 30 detik
echo.
echo Untuk mengecek log generator telemetri:
echo   docker logs -f infrahub-data-generator
echo ========================================================
pause
