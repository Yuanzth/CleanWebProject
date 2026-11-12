@echo off
:: Script untuk menjalankan InfraHub, ITAssetTrack, dan Telemetry Simulator
:: Pastikan Environment .NET 8 dan Python (dengan pyodbc) sudah terinstall

echo --- Memulai InfraHub Web Server (Port 5000) ---
start cmd /k "cd /d D:\Kuliah\Kelas 4 Semester 1\CleanWebProject\InfraHub.Web && dotnet run --urls=\"http://localhost:5000\""

echo --- Memulai ITAssetTrack Web Server (Port 5001) ---
start cmd /k "cd /d D:\Kuliah\Kelas 4 Semester 1\CleanWebProject\ITAssetTrack\ITAssetTrack.Web && dotnet run --urls=\"http://localhost:5001\""

echo --- Memulai Telemetry Simulator ---
start cmd /k "cd /d D:\Kuliah\Kelas 4 Semester 1\CleanWebProject\services && python infrahub_simulator.py"

echo Semua service telah dijalankan di jendela baru.
pause
