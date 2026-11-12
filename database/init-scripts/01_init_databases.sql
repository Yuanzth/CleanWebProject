-- ========================================================
-- 01_init_databases.sql
-- Inisialisasi Database db_infrahub dan db_itassettrack
-- ========================================================
USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'db_infrahub')
BEGIN
    CREATE DATABASE [db_infrahub];
    PRINT 'Database db_infrahub berhasil dibuat.';
END
ELSE
BEGIN
    PRINT 'Database db_infrahub sudah ada.';
END
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'db_itassettrack')
BEGIN
    CREATE DATABASE [db_itassettrack];
    PRINT 'Database db_itassettrack berhasil dibuat.';
END
ELSE
BEGIN
    PRINT 'Database db_itassettrack sudah ada.';
END
GO
