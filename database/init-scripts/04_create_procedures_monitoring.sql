USE [db_infrahub];
GO
IF COL_LENGTH('dbo.tbl_s_unregistered_devices', 'asset_type_id') IS NULL
    ALTER TABLE dbo.tbl_s_unregistered_devices ADD asset_type_id UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH('dbo.tbl_s_unregistered_devices', 'fleet_type') IS NULL
    ALTER TABLE dbo.tbl_s_unregistered_devices ADD fleet_type NVARCHAR(50) NULL;
GO
UPDATE dbo.tbl_s_unregistered_devices
SET asset_type_id = 'FBF7C4C7-B385-4350-A5C5-63C516802CCA',
    fleet_type = 'LTE Modem Unit'
WHERE asset_type_id IS NULL;
GO

USE [db_infrahub];
GO
IF COL_LENGTH('dbo.tbl_t_fleet_tracking_logs', 'signal_quality') IS NULL
    ALTER TABLE dbo.tbl_t_fleet_tracking_logs ADD signal_quality NVARCHAR(20) NULL;
GO
IF COL_LENGTH('dbo.tbl_t_tower_logs', 'dust_level') IS NULL
    ALTER TABLE dbo.tbl_t_tower_logs ADD dust_level DECIMAL(6,2) NULL;
GO
IF COL_LENGTH('dbo.tbl_t_tower_logs', 'luminosity') IS NULL
    ALTER TABLE dbo.tbl_t_tower_logs ADD luminosity DECIMAL(8,2) NULL;
GO
IF COL_LENGTH('dbo.tbl_t_tower_logs', 'spd_status') IS NULL
    ALTER TABLE dbo.tbl_t_tower_logs ADD spd_status NVARCHAR(30) NULL;
GO

USE [db_infrahub];
GO

-- === 04_stored_procedure.sql ===

GO

-- =============================================
-- REPORTING STORED PROCEDURES
-- Untuk page /reports dan endpoint /api/report/summary
-- =============================================

IF OBJECT_ID('[dbo].[sp_ReportGetSiteFilters]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReportGetSiteFilters];
GO
CREATE PROCEDURE [dbo].[sp_ReportGetSiteFilters]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id, site_code, site_name
    FROM tbl_m_sites
    ORDER BY site_code;
END;
GO

IF OBJECT_ID('[dbo].[sp_ReportSummaryAggregate]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReportSummaryAggregate];
GO
CREATE PROCEDURE [dbo].[sp_ReportSummaryAggregate]
    @SiteID UNIQUEIDENTIFIER = NULL,
    @DateFrom DATE,
    @DateTo DATE,
    @VoltageMin FLOAT = 215,
    @VoltageMax FLOAT = 225,
    @FrequencyMin FLOAT = 49.5,
    @FrequencyMax FLOAT = 50.5,
    @PfMin FLOAT = 0.85,
    @FuelCritical FLOAT = 20,
    @FuelWarning FLOAT = 35,
    @WindThreshold FLOAT = 50,
    @WindCritical FLOAT = 80,
    @RainThreshold FLOAT = 50,
    @TiltWarn FLOAT = 0.8,
    @TiltLimit FLOAT = 2.0,
    @LightningLimit FLOAT = 20
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDT DATETIME = CAST(@DateFrom AS DATETIME);
    DECLARE @ToDT   DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@DateTo AS DATETIME)));

    -- ─── Aggregate metrics (rata-rata keseluruhan, tidak perlu dedup) ───────
    DECLARE @total_sites INT, @total_devices INT,
            @avg_voltage FLOAT, @avg_current FLOAT, @avg_power_kw FLOAT,
            @avg_frequency FLOAT, @avg_power_factor FLOAT, @total_energy_kwh FLOAT,
            @anomaly_count INT,
            @avg_tilt FLOAT, @max_tilt FLOAT,
            @avg_ground_resistance FLOAT, @max_ground_resistance FLOAT,
            @total_spd_events INT, @max_lightning_current FLOAT,
            @avg_wind_speed FLOAT, @max_wind_speed FLOAT,
            @avg_rainfall FLOAT, @max_rainfall FLOAT,
            @max_lightning_kA FLOAT, @total_lightning INT,
            @critical_sites INT;

    -- Incident counters (HASIL DEDUP PER DEVICE-HARI)
    DECLARE @fuel_critical INT = 0, @fuel_warning INT = 0,
            @pln_critical INT = 0, @pln_warning INT = 0,
            @wind_critical INT = 0, @wind_warning INT = 0,
            @rain_critical INT = 0, @rain_warning INT = 0,
            @lightning_critical INT = 0, @lightning_warning INT = 0,
            @tilt_critical INT = 0, @tilt_warning INT = 0;

    SELECT @total_sites = COUNT(*)
    FROM tbl_m_sites s
    WHERE (@SiteID IS NULL OR s.id = @SiteID);

    SELECT @total_devices = COUNT(*)
    FROM tbl_m_devices d
    JOIN tbl_m_zones z ON z.id = d.zone_id
    WHERE (@SiteID IS NULL OR z.site_id = @SiteID);

    -- ─── Genset aggregate (rata-rata untuk metric tiles, bukan incident) ─────
    ;WITH G AS (
        SELECT g.*
        FROM tbl_t_genset_logs g
        JOIN tbl_m_devices d ON d.id = g.device_id
        JOIN tbl_m_zones z   ON z.id = d.zone_id
        WHERE g.device_time BETWEEN @FromDT AND @ToDT
          AND (@SiteID IS NULL OR z.site_id = @SiteID)
    )
    SELECT
        @avg_voltage      = ISNULL(AVG(CAST(voltage      AS FLOAT)), 0),
        @avg_current      = ISNULL(AVG(CAST([current]    AS FLOAT)), 0),
        @avg_power_kw     = ISNULL(AVG(CAST(power_kw     AS FLOAT)), 0),
        @avg_frequency    = ISNULL(AVG(CAST(frequency    AS FLOAT)), 0),
        @avg_power_factor = ISNULL(AVG(CAST(power_factor AS FLOAT)), 0),
        @total_energy_kwh = ISNULL(SUM(CAST(energy_kwh   AS FLOAT)), 0),
        @anomaly_count    = ISNULL(SUM(CASE WHEN
            (voltage       IS NOT NULL AND (voltage       < @VoltageMin  OR voltage       > @VoltageMax))  OR
            (frequency     IS NOT NULL AND (frequency     < @FrequencyMin OR frequency     > @FrequencyMax)) OR
            (power_factor  IS NOT NULL AND  power_factor  < @PfMin)
        THEN 1 ELSE 0 END), 0)
    FROM G;

    -- ─── GENSET INCIDENTS — deduplikasi per (device_id, hari) ───────────────
    --
    --   GensetByDay: ambil nilai terparah per device per hari
    --     min_fuel    → nilai terendah fuel hari itu (worst case)
    --     min_voltage → nilai terendah voltage hari itu
    --
    --   Lalu COUNT berapa device-hari yang melewati threshold.
    --   Filter 1 hari → maks N devices (bukan N×96 rows).
    -- ─────────────────────────────────────────────────────────────────────────
    ;WITH G2 AS (
        SELECT g.device_id, g.fuel_level, g.voltage, g.device_time
        FROM tbl_t_genset_logs g
        JOIN tbl_m_devices d ON d.id = g.device_id
        JOIN tbl_m_zones z   ON z.id = d.zone_id
        WHERE g.device_time BETWEEN @FromDT AND @ToDT
          AND (@SiteID IS NULL OR z.site_id = @SiteID)
    ),
    GensetByDay AS (
        SELECT
            device_id,
            CAST(device_time AS DATE) AS log_date,
            MIN(CAST(fuel_level  AS FLOAT)) AS min_fuel,
            MIN(CAST(voltage     AS FLOAT)) AS min_voltage
        FROM G2
        GROUP BY device_id, CAST(device_time AS DATE)
    )
    SELECT
        @fuel_critical = ISNULL(COUNT(CASE WHEN min_fuel < @FuelCritical THEN 1 END), 0),
        @fuel_warning  = ISNULL(COUNT(CASE WHEN min_fuel >= @FuelCritical AND min_fuel < @FuelWarning THEN 1 END), 0),
        @pln_critical  = ISNULL(COUNT(CASE WHEN min_voltage IS NOT NULL AND min_voltage < 50 THEN 1 END), 0),
        @pln_warning   = ISNULL(COUNT(CASE WHEN min_voltage IS NOT NULL AND min_voltage >= 50
                                            AND (min_voltage < @VoltageMin OR min_voltage > @VoltageMax) THEN 1 END), 0)
    FROM GensetByDay;

    -- ─── Tower aggregate (rata-rata) ─────────────────────────────────────────
    ;WITH T AS (
        SELECT t.*
        FROM tbl_t_tower_logs t
        JOIN tbl_m_devices d ON d.id = t.device_id
        JOIN tbl_m_zones z   ON z.id = d.zone_id
        WHERE t.device_time BETWEEN @FromDT AND @ToDT
          AND (@SiteID IS NULL OR z.site_id = @SiteID)
    )
    SELECT
        @avg_tilt              = ISNULL(AVG((ABS(CAST(tilt_x AS FLOAT)) + ABS(CAST(tilt_y AS FLOAT))) / 2.0), 0),
        @max_tilt              = ISNULL(MAX((ABS(CAST(tilt_x AS FLOAT)) + ABS(CAST(tilt_y AS FLOAT))) / 2.0), 0),
        @avg_ground_resistance = ISNULL(AVG(CAST(ground_resistance AS FLOAT)), 0),
        @max_ground_resistance = ISNULL(MAX(CAST(ground_resistance AS FLOAT)), 0),
        @total_spd_events      = ISNULL(SUM(ISNULL(spd_count, 0)), 0),
        @max_lightning_current = ISNULL(MAX(CAST(lightning_current AS FLOAT)), 0),
        @avg_wind_speed        = ISNULL(AVG(CAST(wind_speed  AS FLOAT)), 0),
        @max_wind_speed        = ISNULL(MAX(CAST(wind_speed  AS FLOAT)), 0),
        @avg_rainfall          = ISNULL(AVG(CAST(rainfall    AS FLOAT)), 0),
        @max_rainfall          = ISNULL(MAX(CAST(rainfall    AS FLOAT)), 0),
        @max_lightning_kA      = ISNULL(MAX(CAST(lightning_current AS FLOAT)), 0),
        @total_lightning       = ISNULL(SUM(CASE WHEN ISNULL(lightning_current, 0) > 0 THEN 1 ELSE 0 END), 0)
    FROM T;

    -- ─── TOWER INCIDENTS — deduplikasi per (device_id, hari) ─────────────────
    --
    --   TowerByDay: ambil nilai terparah per device per hari
    --     max_wind         → angin kencang tertinggi
    --     max_rain         → hujan lebat tertinggi
    --     max_lightning    → sambaran terkuat
    --     max_tilt_val     → kemiringan terburuk
    --
    -- ─────────────────────────────────────────────────────────────────────────
    ;WITH T2 AS (
        SELECT t.device_id, t.wind_speed, t.rainfall, t.lightning_current,
               t.tilt_x, t.tilt_y, t.device_time
        FROM tbl_t_tower_logs t
        JOIN tbl_m_devices d ON d.id = t.device_id
        JOIN tbl_m_zones z   ON z.id = d.zone_id
        WHERE t.device_time BETWEEN @FromDT AND @ToDT
          AND (@SiteID IS NULL OR z.site_id = @SiteID)
    ),
    TowerByDay AS (
        SELECT
            device_id,
            CAST(device_time AS DATE) AS log_date,
            MAX(CAST(wind_speed       AS FLOAT))                                                 AS max_wind,
            MAX(CAST(rainfall         AS FLOAT))                                                 AS max_rain,
            MAX(ISNULL(CAST(lightning_current AS FLOAT), 0))                                     AS max_lightning,
            MAX((ABS(ISNULL(CAST(tilt_x AS FLOAT), 0)) + ABS(ISNULL(CAST(tilt_y AS FLOAT), 0))) / 2.0) AS max_tilt_val
        FROM T2
        GROUP BY device_id, CAST(device_time AS DATE)
    )
    SELECT
        @wind_critical      = ISNULL(COUNT(CASE WHEN max_wind >= @WindCritical THEN 1 END), 0),
        @wind_warning       = ISNULL(COUNT(CASE WHEN max_wind >= @WindThreshold AND max_wind < @WindCritical THEN 1 END), 0),
        @rain_critical      = ISNULL(COUNT(CASE WHEN max_rain > 80 THEN 1 END), 0),
        @rain_warning       = ISNULL(COUNT(CASE WHEN max_rain > @RainThreshold AND max_rain <= 80 THEN 1 END), 0),
        @lightning_critical = ISNULL(COUNT(CASE WHEN max_lightning >= @LightningLimit THEN 1 END), 0),
        @lightning_warning  = ISNULL(COUNT(CASE WHEN max_lightning > 0 AND max_lightning < @LightningLimit THEN 1 END), 0),
        @tilt_critical      = ISNULL(COUNT(CASE WHEN max_tilt_val > @TiltLimit THEN 1 END), 0),
        @tilt_warning       = ISNULL(COUNT(CASE WHEN max_tilt_val > @TiltWarn AND max_tilt_val <= @TiltLimit THEN 1 END), 0)
    FROM TowerByDay;

    -- ─── Critical sites count ─────────────────────────────────────────────────
    ;WITH LatestG AS (
        SELECT z.site_id, g.fuel_level, g.voltage,
               ROW_NUMBER() OVER (PARTITION BY g.device_id ORDER BY g.device_time DESC) AS rn
        FROM tbl_t_genset_logs g
        JOIN tbl_m_devices d ON d.id = g.device_id
        JOIN tbl_m_zones z   ON z.id = d.zone_id
        WHERE g.device_time BETWEEN @FromDT AND @ToDT
          AND (@SiteID IS NULL OR z.site_id = @SiteID)
    ),
    LatestT AS (
        SELECT z.site_id, t.wind_speed, t.rainfall, t.lightning_current,
               ((ABS(CAST(t.tilt_x AS FLOAT)) + ABS(CAST(t.tilt_y AS FLOAT))) / 2.0) AS tilt_val,
               ROW_NUMBER() OVER (PARTITION BY t.device_id ORDER BY t.device_time DESC) AS rn
        FROM tbl_t_tower_logs t
        JOIN tbl_m_devices d ON d.id = t.device_id
        JOIN tbl_m_zones z   ON z.id = d.zone_id
        WHERE t.device_time BETWEEN @FromDT AND @ToDT
          AND (@SiteID IS NULL OR z.site_id = @SiteID)
    ),
    CriticalSites AS (
        SELECT DISTINCT site_id FROM LatestG
        WHERE rn = 1 AND (fuel_level < @FuelCritical OR voltage < 50)
        UNION
        SELECT DISTINCT site_id FROM LatestT
        WHERE rn = 1 AND (
            wind_speed >= @WindCritical OR rainfall > 80
            OR ISNULL(lightning_current, 0) >= @LightningLimit
            OR tilt_val > @TiltLimit
        )
    )
    SELECT @critical_sites = COUNT(*) FROM CriticalSites;

    -- ─── Final output ─────────────────────────────────────────────────────────
    SELECT
        @total_sites            AS total_sites,
        @total_devices          AS total_devices,
        @avg_voltage            AS avg_voltage,
        @avg_current            AS avg_current,
        @avg_power_kw           AS avg_power_kw,
        @avg_frequency          AS avg_frequency,
        @avg_power_factor       AS avg_power_factor,
        @total_energy_kwh       AS total_energy_kwh,
        @anomaly_count          AS anomaly_count,
        @avg_tilt               AS avg_tilt,
        @max_tilt               AS max_tilt,
        @avg_ground_resistance  AS avg_ground_resistance,
        @max_ground_resistance  AS max_ground_resistance,
        @total_spd_events       AS total_spd_events,
        @max_lightning_current  AS max_lightning_current,
        @avg_wind_speed         AS avg_wind_speed,
        @max_wind_speed         AS max_wind_speed,
        @avg_rainfall           AS avg_rainfall,
        @max_rainfall           AS max_rainfall,
        @max_lightning_kA       AS max_lightning_kA,
        @total_lightning        AS total_lightning,
        @fuel_critical          AS fuel_critical,
        @fuel_warning           AS fuel_warning,
        @pln_critical           AS pln_critical,
        @pln_warning            AS pln_warning,
        @wind_critical          AS wind_critical,
        @wind_warning           AS wind_warning,
        @rain_critical          AS rain_critical,
        @rain_warning           AS rain_warning,
        @lightning_critical     AS lightning_critical,
        @lightning_warning      AS lightning_warning,
        @tilt_critical          AS tilt_critical,
        @tilt_warning           AS tilt_warning,
        @critical_sites         AS critical_sites,
        CASE WHEN @total_sites - @critical_sites > 0
             THEN @total_sites - @critical_sites
             ELSE 0
        END                     AS nominal_sites;
END;
GO

-- =============================================
-- sp_ReportAttentionTowers — tidak ada perubahan
-- =============================================
IF OBJECT_ID('[dbo].[sp_ReportAttentionTowers]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReportAttentionTowers];
GO
CREATE PROCEDURE [dbo].[sp_ReportAttentionTowers]
    @SiteID UNIQUEIDENTIFIER = NULL,
    @DateFrom DATE,
    @DateTo DATE,
    @TiltWarn FLOAT = 0.8,
    @TopN INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDT DATETIME = CAST(@DateFrom AS DATETIME);
    DECLARE @ToDT   DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@DateTo AS DATETIME)));

    SELECT TOP (@TopN)
        s.site_code,
        MAX((ABS(CAST(t.tilt_x AS FLOAT)) + ABS(CAST(t.tilt_y AS FLOAT))) / 2.0) AS max_tilt,
        AVG(CAST(t.ground_resistance AS FLOAT))                                    AS avg_ground,
        SUM(ISNULL(t.spd_count, 0))                                                AS spd
    FROM tbl_t_tower_logs t
    JOIN tbl_m_devices d ON d.id = t.device_id
    JOIN tbl_m_zones z   ON z.id = d.zone_id
    JOIN tbl_m_sites s   ON s.id = z.site_id
    WHERE t.device_time BETWEEN @FromDT AND @ToDT
      AND (@SiteID IS NULL OR s.id = @SiteID)
    GROUP BY s.site_code
    HAVING MAX((ABS(CAST(t.tilt_x AS FLOAT)) + ABS(CAST(t.tilt_y AS FLOAT))) / 2.0) > @TiltWarn
    ORDER BY max_tilt DESC;
END;
GO

IF OBJECT_ID('[dbo].[sp_ReportRecentGenset]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReportRecentGenset];
GO
CREATE PROCEDURE [dbo].[sp_ReportRecentGenset]
    @SiteID UNIQUEIDENTIFIER = NULL,
    @DateFrom DATE,
    @DateTo DATE,
    @TopN INT = 120
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDT DATETIME = CAST(@DateFrom AS DATETIME);
    DECLARE @ToDT DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@DateTo AS DATETIME)));

    SELECT TOP (@TopN)
        s.site_code,
        d.device_name,
        g.device_time,
        g.fuel_level,
        g.voltage
    FROM tbl_t_genset_logs g
    JOIN tbl_m_devices d ON d.id = g.device_id
    JOIN tbl_m_zones z ON z.id = d.zone_id
    JOIN tbl_m_sites s ON s.id = z.site_id
    WHERE g.device_time BETWEEN @FromDT AND @ToDT
      AND (@SiteID IS NULL OR s.id = @SiteID)
    ORDER BY g.device_time DESC;
END;
GO

IF OBJECT_ID('[dbo].[sp_ReportRecentTower]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReportRecentTower];
GO
CREATE PROCEDURE [dbo].[sp_ReportRecentTower]
    @SiteID UNIQUEIDENTIFIER = NULL,
    @DateFrom DATE,
    @DateTo DATE,
    @TopN INT = 120
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDT DATETIME = CAST(@DateFrom AS DATETIME);
    DECLARE @ToDT DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@DateTo AS DATETIME)));

    SELECT TOP (@TopN)
        s.site_code,
        d.device_name,
        t.device_time,
        t.wind_speed,
        t.rainfall,
        t.spd_count,
        t.lightning_current,
        t.tilt_x,
        t.tilt_y
    FROM tbl_t_tower_logs t
    JOIN tbl_m_devices d ON d.id = t.device_id
    JOIN tbl_m_zones z ON z.id = d.zone_id
    JOIN tbl_m_sites s ON s.id = z.site_id
    WHERE t.device_time BETWEEN @FromDT AND @ToDT
      AND (@SiteID IS NULL OR s.id = @SiteID)
    ORDER BY t.device_time DESC;
END;
GO

IF OBJECT_ID('[dbo].[sp_ReportTrendRaw]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReportTrendRaw];
GO
CREATE PROCEDURE [dbo].[sp_ReportTrendRaw]
    @Metric NVARCHAR(50),
    @SiteID UNIQUEIDENTIFIER = NULL,
    @StartDT DATETIME,
    @EndDT DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MetricNorm NVARCHAR(50) = LOWER(LTRIM(RTRIM(ISNULL(@Metric, ''))));

    IF @MetricNorm = 'voltage'
    BEGIN
        SELECT
            g.device_time,
            CAST(g.voltage AS FLOAT) AS metric_value
        FROM tbl_t_genset_logs g
        JOIN tbl_m_devices d ON d.id = g.device_id
        JOIN tbl_m_zones z ON z.id = d.zone_id
        WHERE g.device_time BETWEEN @StartDT AND @EndDT
          AND g.voltage IS NOT NULL
          AND (@SiteID IS NULL OR z.site_id = @SiteID);
        RETURN;
    END

    IF @MetricNorm = 'wind_speed'
    BEGIN
        SELECT
            t.device_time,
            CAST(t.wind_speed AS FLOAT) AS metric_value
        FROM tbl_t_tower_logs t
        JOIN tbl_m_devices d ON d.id = t.device_id
        JOIN tbl_m_zones z ON z.id = d.zone_id
        WHERE t.device_time BETWEEN @StartDT AND @EndDT
          AND t.wind_speed IS NOT NULL
          AND (@SiteID IS NULL OR z.site_id = @SiteID);
        RETURN;
    END

    SELECT CAST(NULL AS DATETIME) AS device_time, CAST(NULL AS FLOAT) AS metric_value WHERE 1 = 0;
END;
GO

IF OBJECT_ID('[dbo].[sp_ReportTrendPerDevice]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReportTrendPerDevice];
GO
CREATE PROCEDURE [dbo].[sp_ReportTrendPerDevice]
    @Metric NVARCHAR(50),
    @SiteID UNIQUEIDENTIFIER = NULL,
    @StartDT DATETIME,
    @EndDT DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MetricNorm NVARCHAR(50) = LOWER(LTRIM(RTRIM(ISNULL(@Metric, ''))));

    IF @MetricNorm IN ('voltage', 'fuel_level', 'luminosity')
    BEGIN
        SELECT
            g.device_time,
            z.zone_code AS zone_name,
            CAST(
                CASE
                    WHEN @MetricNorm = 'voltage' THEN g.voltage
                    WHEN @MetricNorm = 'fuel_level' THEN g.fuel_level
                    WHEN @MetricNorm = 'luminosity' THEN g.illuminance
                END
            AS FLOAT) AS metric_value
        FROM tbl_t_genset_logs g
        JOIN tbl_m_devices d ON d.id = g.device_id
        JOIN tbl_m_zones z ON z.id = d.zone_id
        WHERE g.device_time BETWEEN @StartDT AND @EndDT
          AND (@SiteID IS NULL OR z.site_id = @SiteID)
          AND (
                (@MetricNorm = 'voltage' AND g.voltage IS NOT NULL)
             OR (@MetricNorm = 'fuel_level' AND g.fuel_level IS NOT NULL)
             OR (@MetricNorm = 'luminosity' AND g.illuminance IS NOT NULL)
          );
        RETURN;
    END

        IF @MetricNorm IN ('wind_speed', 'rainfall', 'ground_resistance', 'lightning_current', 'temperature', 'dust_level', 'tilt')
    BEGIN
        SELECT
            t.device_time,
            z.zone_code AS zone_name,
            CAST(
                CASE
                    WHEN @MetricNorm = 'wind_speed' THEN t.wind_speed
                    WHEN @MetricNorm = 'rainfall' THEN t.rainfall
                    WHEN @MetricNorm = 'ground_resistance' THEN t.ground_resistance
                    WHEN @MetricNorm = 'lightning_current' THEN t.lightning_current
                    WHEN @MetricNorm = 'temperature' THEN t.temperature
                    WHEN @MetricNorm = 'dust_level' THEN t.pm10
                    WHEN @MetricNorm = 'tilt' THEN ((ABS(ISNULL(t.tilt_x, 0)) + ABS(ISNULL(t.tilt_y, 0))) / 2.0)
                END
            AS FLOAT) AS metric_value
        FROM tbl_t_tower_logs t
        JOIN tbl_m_devices d ON d.id = t.device_id
        JOIN tbl_m_zones z ON z.id = d.zone_id
        WHERE t.device_time BETWEEN @StartDT AND @EndDT
          AND (@SiteID IS NULL OR z.site_id = @SiteID)
          AND (
                (@MetricNorm = 'wind_speed' AND t.wind_speed IS NOT NULL)
             OR (@MetricNorm = 'rainfall' AND t.rainfall IS NOT NULL)
             OR (@MetricNorm = 'ground_resistance' AND t.ground_resistance IS NOT NULL)
             OR (@MetricNorm = 'lightning_current' AND t.lightning_current IS NOT NULL)
             OR (@MetricNorm = 'temperature' AND t.temperature IS NOT NULL)
             OR (@MetricNorm = 'dust_level' AND t.pm10 IS NOT NULL)
             OR (@MetricNorm = 'tilt' AND (t.tilt_x IS NOT NULL OR t.tilt_y IS NOT NULL))
          );
        RETURN;
    END

    SELECT CAST(NULL AS DATETIME) AS device_time, CAST(NULL AS NVARCHAR(50)) AS zone_name, CAST(NULL AS FLOAT) AS metric_value WHERE 1 = 0;
END;
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- 1. SP Get Realtime Genset Status
-- Update: Hapus Join tbl_r_areas, Hapus @AreaID, Sesuaikan Hierarki Site -> Zone
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetRealtimeGensetStatus]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetRealtimeGensetStatus];
GO
CREATE PROCEDURE [dbo].[sp_GetRealtimeGensetStatus]
    @DeviceID UNIQUEIDENTIFIER = NULL,
    @View NVARCHAR(MAX) = '*',
    @SiteID UNIQUEIDENTIFIER = NULL,
    @ZoneID UNIQUEIDENTIFIER = NULL 
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @SQL NVARCHAR(MAX);
    DECLARE @ColumnList NVARCHAR(MAX);

    SET @View = LOWER(ISNULL(TRIM(@View), '*'));

    -- Update Column List: Menghapus a.area_name dan menyesuaikan sumber tabel
    IF @View = '*' OR @View = ''
        SET @ColumnList = 'g.[id], g.[device_id], d.[device_name], d.[is_active], z.[id] AS [zone_id], z.[zone_name], s.[site_name], 
                           g.[voltage], g.[current] AS ampere, g.[power_kw] AS watt, g.[energy_kwh] AS energy, 
                           g.[frequency], g.[power_factor] AS powerfactor, g.[fuel_level], 
                           g.[illuminance] AS luminosity, g.[device_time]';
    ELSE
        SET @ColumnList = 'g.[device_id], d.[device_name], d.[is_active], z.[id] AS [zone_id], z.[zone_name], s.[site_name], g.[' + @View + '], g.[device_time]';

    SET @SQL = '
    SELECT * FROM (
        SELECT ' + @ColumnList + ',
            ROW_NUMBER() OVER (PARTITION BY g.device_id ORDER BY g.device_time DESC) as rn
        FROM tbl_t_genset_logs g
        JOIN tbl_m_devices d ON g.device_id = d.id
        JOIN tbl_m_zones z ON d.zone_id = z.id    
        JOIN tbl_m_sites s ON z.site_id = s.id    
        WHERE 1=1 ' 
        + CASE WHEN @DeviceID IS NOT NULL THEN ' AND g.device_id = @pID' ELSE '' END
        + CASE WHEN @SiteID IS NOT NULL THEN ' AND z.site_id = @pSite' ELSE '' END
        + CASE WHEN @ZoneID IS NOT NULL THEN ' AND d.zone_id = @pZone' ELSE '' END 
    + ') tmp
    WHERE rn = 1
    ORDER BY device_time DESC';

    EXEC sp_executesql @SQL, 
        N'@pID UNIQUEIDENTIFIER, @pSite UNIQUEIDENTIFIER, @pZone UNIQUEIDENTIFIER', 
        @pID = @DeviceID, @pSite = @SiteID, @pZone = @ZoneID;
END;
GO

-- =============================================
-- 2. SP Get Realtime Tower Status
-- Update: Hapus Area, Tambah Parameter Cuaca & Lingkungan Lengkap
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetRealtimeTowerStatus]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetRealtimeTowerStatus];
GO
CREATE PROCEDURE [dbo].[sp_GetRealtimeTowerStatus]
    @DeviceID UNIQUEIDENTIFIER = NULL,
    @View NVARCHAR(MAX) = '*',
    @SiteID UNIQUEIDENTIFIER = NULL,
    @ZoneID UNIQUEIDENTIFIER = NULL 
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @SQL NVARCHAR(MAX);
    DECLARE @ColumnList NVARCHAR(MAX);

    SET @View = LOWER(ISNULL(TRIM(@View), '*'));

    -- Update Column List: Menambahkan parameter ETL baru bersanding dengan update teman Anda
    IF @View = '*' OR @View = ''
        SET @ColumnList = 't.[id], t.[device_id], d.[device_name], d.[is_active], z.[id] AS [zone_id], z.[zone_name], s.[site_name], 
                           d.[asset_height],
                           t.[latitude], t.[longitude], 
                           t.[wind_speed], t.[wind_direction], t.[wind_cardinal], t.[temperature], t.[humidity], 
                           t.[pressure], t.[rainfall] AS precipitation, 
                           t.[illuminance] AS luminosity, t.[solar_radiation], t.[pm2_5], t.[pm10],
                           t.[tilt_x], t.[tilt_y], t.[ground_resistance], 
                           t.[spd_count] AS lightning, t.[lightning_current],
                           t.[deviation_m], t.[weather_status], t.[air_quality],
                           t.[device_time]';
    ELSE
        SET @ColumnList = 't.[device_id], d.[device_name], d.[is_active], z.[id] AS [zone_id], z.[zone_name], s.[site_name], 
                           d.[asset_height], t.[latitude], t.[longitude], 
                           t.[' + @View + '], t.[device_time]';

    SET @SQL = '
    SELECT * FROM (
        SELECT ' + @ColumnList + ',
            ROW_NUMBER() OVER (PARTITION BY t.device_id ORDER BY t.device_time DESC) as rn
        FROM tbl_t_tower_logs t
        JOIN tbl_m_devices d ON t.device_id = d.id
        JOIN tbl_m_zones z ON d.zone_id = z.id    
        JOIN tbl_m_sites s ON z.site_id = s.id    
        WHERE 1=1 ' 
        + CASE WHEN @DeviceID IS NOT NULL THEN ' AND t.device_id = @pID' ELSE '' END
        + CASE WHEN @SiteID IS NOT NULL THEN ' AND z.site_id = @pSite' ELSE '' END
        + CASE WHEN @ZoneID IS NOT NULL THEN ' AND d.zone_id = @pZone' ELSE '' END +
    ') tmp
    WHERE rn = 1
    ORDER BY device_time DESC';

    EXEC sp_executesql @SQL, 
        N'@pID UNIQUEIDENTIFIER, @pSite UNIQUEIDENTIFIER, @pZone UNIQUEIDENTIFIER', 
        @pID = @DeviceID, @pSite = @SiteID, @pZone = @ZoneID;
END;
GO

-- =============================================
-- 3. SP Get Registered Devices
-- Update: Hapus Area, Tambah Filter Zone, Tambah Kolom IMEI & Phone
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetRegisteredDevices]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetRegisteredDevices];
GO
CREATE PROCEDURE [dbo].[sp_GetRegisteredDevices]
    @SiteID UNIQUEIDENTIFIER = NULL,
    @ZoneID UNIQUEIDENTIFIER = NULL, 
    @IsActive BIT = NULL,
    @Search NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        d.id,
        d.serial_number,
        d.imei,             -- Kolom Baru
        d.phone_number,      -- Kolom Baru
        d.device_name,
        d.asset_height,      -- TAMBAHAN: Membaca tinggi fisik tower
        d.is_active,
        z.zone_code,        -- Tampilkan Kode Zona
        z.zone_name,        -- Tampilkan Nama Zona
        s.site_code,        -- Tampilkan Kode Site
        s.site_name,        -- Tampilkan Nama Site (Level Tertinggi)
        at.type_name as asset_type,
        d.created_at
    FROM tbl_m_devices d
    JOIN tbl_m_zones z ON d.zone_id = z.id    -- Join Device ke Zone
    JOIN tbl_m_sites s ON z.site_id = s.id    -- Join Zone ke Site
    JOIN tbl_r_asset_types at ON d.asset_type_id = at.id
    WHERE 
        (@SiteID IS NULL OR z.site_id = @SiteID) AND
        (@ZoneID IS NULL OR d.zone_id = @ZoneID) AND 
        (@IsActive IS NULL OR d.is_active = @IsActive) AND
        (
            @Search IS NULL OR 
            d.device_name LIKE '%' + @Search + '%' OR 
            d.serial_number LIKE '%' + @Search + '%' OR
            d.imei LIKE '%' + @Search + '%' OR
            d.phone_number LIKE '%' + @Search + '%'
        )
    ORDER BY s.site_code, z.zone_code, d.device_name;
END;
GO

-- =============================================
-- 4. SP Get Unregistered Devices
-- Tidak ada perubahan relasi, disesuaikan dengan skema tabel terbaru
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetUnregisteredDevices]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetUnregisteredDevices];
GO
CREATE PROCEDURE [dbo].[sp_GetUnregisteredDevices]
    @SerialNumber NVARCHAR(100) = NULL,
    @Search NVARCHAR(100) = NULL,
    @AssetTypeID UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        ud.id,
        ud.serial_number,
        ud.asset_type_id,
        ud.fleet_type,
        ud.raw_payload,
        ud.last_seen,
        ud.created_at,
        ud.created_by,
        ud.updated_at,
        ud.updated_by,
        ISNULL(at.type_name, 'Unknown') AS asset_type_name
    FROM tbl_s_unregistered_devices ud
    LEFT JOIN tbl_r_asset_types at ON ud.asset_type_id = at.id
    WHERE 
        (@SerialNumber IS NULL OR ud.serial_number = @SerialNumber) AND
        (@Search IS NULL OR ud.serial_number LIKE '%' + @Search + '%') AND
        (@AssetTypeID IS NULL OR ud.asset_type_id = @AssetTypeID)
    ORDER BY ud.last_seen DESC;
END;
GO

-- =============================================
-- 5. SP Get Sidebar Site Stats
-- Update: Hapus tabel Area, Site jadi entitas tertinggi
-- Menghitung status perangkat di semua zona dalam satu Site
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetSidebarSiteStats]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetSidebarSiteStats];
GO
CREATE PROCEDURE [dbo].[sp_GetSidebarSiteStats]
    @SiteID UNIQUEIDENTIFIER = NULL  -- NULL = semua site (Admin), diisi = filter per site (Viewer)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        s.id AS site_id,
        s.site_code,
        s.site_name,
        s.latitude,
        s.longitude,
        ISNULL(SUM(CASE WHEN d.is_active = 1 THEN 1 ELSE 0 END), 0) AS active_count,
        ISNULL(SUM(CASE WHEN d.is_active = 0 THEN 1 ELSE 0 END), 0) AS inactive_count,
        0 AS maintenance_count 
    FROM tbl_m_sites s
    LEFT JOIN tbl_m_zones z ON s.id = z.site_id
    LEFT JOIN tbl_m_devices d ON z.id = d.zone_id
    WHERE 
        (@SiteID IS NULL OR s.id = @SiteID)  -- Filter: NULL = semua, diisi = spesifik
    GROUP BY 
        s.id, 
        s.site_code, 
        s.site_name, 
        s.latitude, 
        s.longitude
    ORDER BY 
        s.site_code ASC;
END;
GO
-- =============================================
-- 6. SP Get Latest Zone Status
-- Update: Mendapatkan Logss tiap zone tbl_m_zones
-- =============================================

-- ================================================================
-- PATCH A: sp_GetLatestZoneStatus
-- TAMBAH: tower_device_name, generator_device_name,
--         tower_status, generator_status, site_id, area_desc
-- Kolom inilah yang dibaca JS di index.blade.php → updateDeviceInfo()
-- ================================================================
IF OBJECT_ID('[dbo].[sp_GetLatestZoneStatus]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetLatestZoneStatus];
GO
CREATE PROCEDURE [dbo].[sp_GetLatestZoneStatus]
    @ZoneID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TowerTypeID  UNIQUEIDENTIFIER = (SELECT id FROM tbl_r_asset_types WHERE type_name = 'TOWER');
    DECLARE @GensetTypeID UNIQUEIDENTIFIER = (SELECT id FROM tbl_r_asset_types WHERE type_name = 'GENSET');

    SELECT
        z.id            AS zone_id,
        z.zone_name,
        z.site_id,
        s.site_name,
        s.site_code     AS site_code, -- Menggantikan area_desc karena Area dihapus

        -- Ambil Nama Device & Tinggi dari M_DEVICES (Re-integrasi ETL)
        tower_dev.device_name   AS tower_device_name,
        tower_dev.asset_height  AS asset_height,
        gen_dev.device_name     AS generator_device_name,

        -- Status aktif (1/0)
        CASE WHEN tower_dev.is_active = 1 THEN 'active' ELSE 'down' END AS tower_status,
        CASE WHEN gen_dev.is_active   = 1 THEN 'active' ELSE 'down' END AS generator_status,

        -- Koordinat diambil dari log tower terbaru
        ISNULL(t.latitude,  0) AS latitude,
        ISNULL(t.longitude, 0) AS longitude,

        -- Parameter Tower + Kolom Arah Angin (Re-integrasi ETL)
        t.wind_speed, t.wind_direction, t.wind_cardinal, t.ground_resistance, t.tilt_x, t.tilt_y,
        t.humidity, t.temperature, t.spd_count AS lightning,
        t.pm2_5 AS pm2_5,
        t.pm10 AS pm_10,
        t.pm10 AS dust_level,
        t.pm10 AS pm10,
        t.rainfall AS precipitation, t.device_time AS tower_time,

        -- Ambil Hasil Kalkulasi ETL dari Python (Menggantikan CASE WHEN manual)
        t.deviation_m,
        t.air_quality,
        t.weather_status AS weather,

        -- Parameter Genset
        g.voltage, g.[current] AS ampere, g.power_kw AS watt, g.fuel_level,
        g.energy_kwh AS energy, g.frequency, g.power_factor AS powerfactor,
        g.illuminance AS luminosity, g.device_time AS genset_time

    FROM tbl_m_zones z
    LEFT JOIN tbl_m_sites s ON z.site_id  = s.id

    -- Ambil tower terbaru dari zone ini (Ditambahkan d.asset_height)
    OUTER APPLY (
        SELECT TOP 1 d.device_name, d.is_active, d.asset_height 
        FROM tbl_m_devices d
        WHERE d.zone_id = z.id AND d.asset_type_id = @TowerTypeID ORDER BY d.is_active DESC, d.created_at ASC
    ) tower_dev

    -- Ambil genset terbaru dari zone ini
    OUTER APPLY (
        SELECT TOP 1 d.device_name, d.is_active FROM tbl_m_devices d
        WHERE d.zone_id = z.id AND d.asset_type_id = @GensetTypeID ORDER BY d.is_active DESC, d.created_at ASC
    ) gen_dev

    -- Log Sensor Terbaru
    OUTER APPLY (
        SELECT TOP 1 tl.*
        FROM tbl_t_tower_logs tl
        JOIN tbl_m_devices d ON tl.device_id = d.id
        WHERE d.zone_id = z.id
        ORDER BY tl.device_time DESC, tl.created_at DESC, tl.id DESC
    ) t
    OUTER APPLY (
        SELECT TOP 1 gl.*
        FROM tbl_t_genset_logs gl
        JOIN tbl_m_devices d ON gl.device_id = d.id
        WHERE d.zone_id = z.id
        ORDER BY gl.device_time DESC, gl.created_at DESC, gl.id DESC
    ) g

    WHERE z.id = @ZoneID;
END;
GO


-- ================================================================
-- PATCH B: sp_GetSiteZones  (SP BARU)
-- Mengembalikan semua zone milik 1 site beserta:
--   zone_id, zone_name
--   tower_device_name, generator_device_name  → tooltip marker
--   tower_status, generator_status, zone_status → warna marker
--   latitude, longitude                        → posisi marker di map
--
-- Dipanggil oleh endpoint baru:
--   GET /api/monitoring/site-zones?site_id={uuid}
-- ================================================================
IF OBJECT_ID('[dbo].[sp_GetSiteZones]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetSiteZones];
GO
CREATE PROCEDURE [dbo].[sp_GetSiteZones]
    @SiteID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TowerTypeID  UNIQUEIDENTIFIER = (SELECT id FROM tbl_r_asset_types WHERE type_name = 'TOWER');
    DECLARE @GensetTypeID UNIQUEIDENTIFIER = (SELECT id FROM tbl_r_asset_types WHERE type_name = 'GENSET');

    SELECT
        z.id        AS zone_id,
        z.zone_name,
        tower_dev.device_name   AS tower_device_name,
        gen_dev.device_name     AS generator_device_name,
        CASE WHEN tower_dev.is_active = 1 THEN 'active' ELSE 'down' END AS tower_status,
        CASE WHEN gen_dev.is_active   = 1 THEN 'active' ELSE 'down' END AS generator_status,

        -- Status zona gabungan untuk warna marker di peta
        CASE
            WHEN ISNULL(tower_dev.is_active, 0) = 1 AND ISNULL(gen_dev.is_active, 0) = 1 THEN 'active'
            WHEN ISNULL(tower_dev.is_active, 0) = 0 AND ISNULL(gen_dev.is_active, 0) = 0 THEN 'down'
            ELSE 'partial'
        END AS zone_status,

        ISNULL(coord.latitude,  0) AS latitude,
        ISNULL(coord.longitude, 0) AS longitude
    FROM tbl_m_zones z
    OUTER APPLY (
        SELECT TOP 1 d.device_name, d.is_active FROM tbl_m_devices d
        WHERE d.zone_id = z.id AND d.asset_type_id = @TowerTypeID ORDER BY d.is_active DESC, d.created_at ASC
    ) tower_dev
    OUTER APPLY (
        SELECT TOP 1 d.device_name, d.is_active FROM tbl_m_devices d
        WHERE d.zone_id = z.id AND d.asset_type_id = @GensetTypeID ORDER BY d.is_active DESC, d.created_at ASC
    ) gen_dev
    OUTER APPLY (
        SELECT TOP 1 tl.latitude, tl.longitude FROM tbl_t_tower_logs tl
        JOIN tbl_m_devices d ON tl.device_id = d.id
        WHERE d.zone_id = z.id AND tl.latitude <> 0 ORDER BY tl.device_time DESC
    ) coord
    WHERE z.site_id = @SiteID
    ORDER BY z.zone_name ASC;
END;
GO

-- =============================================
-- SP Get Dashboard Anomalies
-- Update: Hapus AreaID & Join Area, Gunakan site_code sebagai penanda Site
-- Return: anomali terformat untuk blade anomaly-card component
-- =============================================

IF OBJECT_ID('[dbo].[sp_GetDashboardAnomalies]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetDashboardAnomalies];
GO
CREATE PROCEDURE [dbo].[sp_GetDashboardAnomalies]
    @SiteID UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @WindWarnThreshold FLOAT = 50;
    DECLARE @WindCritThreshold FLOAT = 80;

    ;WITH LatestGenset AS (
        SELECT gl.*,
               ROW_NUMBER() OVER (PARTITION BY gl.device_id ORDER BY gl.device_time DESC) AS rn
        FROM tbl_t_genset_logs gl
    ),
    LatestTower AS (
        SELECT tl.*,
               ROW_NUMBER() OVER (PARTITION BY tl.device_id ORDER BY tl.device_time DESC) AS rn
        FROM tbl_t_tower_logs tl
    ),
    TodaySpdLatest AS (
        SELECT
            tl.device_id,
            CAST(ISNULL(tl.spd_count, 0) AS INT) AS latest_spd_today,
            ROW_NUMBER() OVER (PARTITION BY tl.device_id ORDER BY tl.device_time DESC) AS rn
        FROM tbl_t_tower_logs tl
        WHERE CAST(tl.device_time AS DATE) = CAST(GETDATE() AS DATE)
    ),
    PrevSpd AS (
        SELECT
            tl.device_id,
            CAST(ISNULL(tl.spd_count, 0) AS INT) AS prev_spd,
            ROW_NUMBER() OVER (PARTITION BY tl.device_id ORDER BY tl.device_time DESC) AS rn
        FROM tbl_t_tower_logs tl
        WHERE tl.device_time < CAST(GETDATE() AS DATE)
    ),
    TodaySpd AS (
        SELECT
            lt.device_id,
            CASE
                WHEN lt.latest_spd_today - ISNULL(ps.prev_spd, 0) < 0
                    THEN lt.latest_spd_today
                ELSE lt.latest_spd_today - ISNULL(ps.prev_spd, 0)
            END AS spd_today_count
        FROM TodaySpdLatest lt
        LEFT JOIN PrevSpd ps ON ps.device_id = lt.device_id AND ps.rn = 1
        WHERE lt.rn = 1
    ),
    AllAnomalies AS (

        -- ── Branch 1: GENERATOR ────────────────────────────────────
        SELECT
            'generator'     AS panel,
            z.id            AS zone_id,
            s.site_code     AS site, 
            d.device_name   AS name,
            CASE
                WHEN lg.fuel_level  < 20                   THEN 'critical'
                WHEN lg.voltage    NOT BETWEEN 198 AND 242 THEN 'critical'
                WHEN lg.frequency  NOT BETWEEN 47  AND 53  THEN 'critical'
                ELSE 'warning'
            END             AS anomaly_status,
            CASE
                WHEN lg.voltage   NOT BETWEEN 198 AND 242  THEN 'electrical'
                WHEN lg.frequency NOT BETWEEN 47  AND 53   THEN 'electrical'
                ELSE 'fuel_gauge'
            END             AS anomaly_type,
            CAST(CAST(ROUND(lg.fuel_level, 0) AS INT) AS NVARCHAR(100)) AS anomaly_value,
            CAST(lg.voltage AS NVARCHAR(10))                             AS meta_voltage,
            CASE WHEN lg.voltage NOT BETWEEN 198 AND 242
                 THEN 'critical' ELSE 'normal' END                       AS meta_voltage_status,
            CAST(CAST(ROUND(lg.frequency,0) AS INT) AS NVARCHAR(10))    AS meta_frequency,
            CASE WHEN lg.frequency NOT BETWEEN 47 AND 53
                 THEN 'critical' ELSE 'normal' END                       AS meta_freq_status,
            CAST(NULL AS DECIMAL(18,1))                                  AS tilt_value,
            CASE
                WHEN lg.fuel_level  < 20                   THEN 0
                WHEN lg.voltage    NOT BETWEEN 198 AND 242 THEN 0
                WHEN lg.frequency  NOT BETWEEN 47  AND 53  THEN 0
                ELSE 1
            END             AS sort_order
        FROM LatestGenset lg
        JOIN tbl_m_devices d ON lg.device_id = d.id
        JOIN tbl_m_zones   z ON d.zone_id    = z.id
        JOIN tbl_m_sites   s ON z.site_id    = s.id
        WHERE lg.rn = 1
          AND d.is_active = 1
          AND (@SiteID IS NULL OR s.id = @SiteID)
          AND (
              lg.fuel_level  < 40
              OR lg.voltage   NOT BETWEEN 198 AND 242
              OR lg.frequency NOT BETWEEN 47  AND 53
          )

        UNION ALL

        -- ── Branch 2: TOWER TILT (Diperbarui dengan ETL Deviasi cm) ──
        SELECT
            'tower',
            z.id,
            s.site_code,
            d.device_name,
            -- Logic: Kritis jika deviasi > 1 Meter (100cm) atau (fallback) kemiringan > 2 derajat
            CASE
                WHEN lt.deviation_m >= 1.0 THEN 'critical'
                WHEN lt.deviation_m >= 0.5 THEN 'warning'
                WHEN lt.deviation_m IS NULL AND (ABS(lt.tilt_x) > 2.0 OR ABS(lt.tilt_y) > 2.0) THEN 'critical'
                ELSE 'warning'
            END,
            'tower',
            -- Tampilkan satuan "cm" jika deviasi meter tersedia, jika tidak tampilkan "°"
            CASE
                WHEN lt.deviation_m IS NOT NULL 
                THEN CAST(CAST(ROUND(lt.deviation_m * 100, 1) AS DECIMAL(18,1)) AS NVARCHAR(100)) + ' cm'
                ELSE CAST(CAST(ROUND(CASE WHEN ABS(lt.tilt_x) >= ABS(lt.tilt_y) THEN ABS(lt.tilt_x) ELSE ABS(lt.tilt_y) END, 1) AS DECIMAL(18,1)) AS NVARCHAR(100)) + '°'
            END,
            NULL, NULL, NULL, NULL,
            CAST(ROUND(CASE WHEN ABS(lt.tilt_x) >= ABS(lt.tilt_y) THEN ABS(lt.tilt_x) ELSE ABS(lt.tilt_y) END, 1) AS DECIMAL(18,1)),
            CASE
                WHEN lt.deviation_m >= 1.0 OR ABS(lt.tilt_x) > 2.0 OR ABS(lt.tilt_y) > 2.0 THEN 0
                ELSE 1
            END
        FROM LatestTower lt
        JOIN tbl_m_devices d ON lt.device_id = d.id
        JOIN tbl_m_zones   z ON d.zone_id    = z.id
        JOIN tbl_m_sites   s ON z.site_id    = s.id
        WHERE lt.rn = 1
          AND d.is_active = 1
          AND (@SiteID IS NULL OR s.id = @SiteID)
          -- Trigger jika deviasi >= 50cm ATAU kemiringan >= 1 Derajat
          AND (lt.deviation_m >= 0.5 OR ABS(lt.tilt_x) > 1.0 OR ABS(lt.tilt_y) > 1.0)

        UNION ALL

        -- ── Branch 3: ENVIRONMENTAL — Angin & Suhu ─────────────────
        SELECT
            'environmental',
            z.id,
            s.site_code,
            d.device_name,
            CASE
                WHEN lt.wind_speed >= @WindCritThreshold OR lt.temperature > 45 THEN 'critical'
                ELSE 'warning'
            END,
            'weather',
            CASE
                WHEN lt.wind_speed >= @WindCritThreshold AND lt.temperature > 45 THEN 'Angin Kencang & Suhu Ekstrem'
                WHEN lt.wind_speed >= @WindCritThreshold                          THEN 'Angin Kencang (>=80 km/h)'
                WHEN lt.temperature > 45                                 THEN 'Suhu Kritis (>45C)'
                WHEN lt.wind_speed >= @WindWarnThreshold AND lt.temperature > 38  THEN 'Angin & Suhu Tinggi'
                WHEN lt.wind_speed >= @WindWarnThreshold                          THEN 'Angin Tinggi (>=50 km/h)'
                ELSE                                                     'Suhu Tinggi (>38C)'
            END,
            NULL, NULL, NULL, NULL, NULL,
            CASE
                WHEN lt.wind_speed >= @WindCritThreshold OR lt.temperature > 45 THEN 0
                ELSE 1
            END
        FROM LatestTower lt
        JOIN tbl_m_devices d ON lt.device_id = d.id
        JOIN tbl_m_zones   z ON d.zone_id    = z.id
        JOIN tbl_m_sites   s ON z.site_id    = s.id
        WHERE lt.rn = 1
          AND d.is_active = 1
          AND (@SiteID IS NULL OR s.id = @SiteID)
          AND (lt.wind_speed >= @WindWarnThreshold OR lt.temperature > 38)

        UNION ALL

        -- ── Branch 4: LIGHTNING / SPD ──────────────────────────────
        SELECT
            'tower',
            z.id,
            s.site_code,
            d.device_name,
            CASE WHEN ts.spd_today_count >= 5 THEN 'critical' ELSE 'warning' END,
            'lightning',
            CASE
                WHEN ts.spd_today_count >= 5
                THEN 'SPD Strike Hari Ini (' + CAST(ts.spd_today_count AS NVARCHAR(10)) + 'x) - Kritis'
                ELSE 'SPD Strike Hari Ini (' + CAST(ts.spd_today_count AS NVARCHAR(10)) + 'x)'
            END,
            NULL, NULL, NULL, NULL, NULL,
            CASE WHEN ts.spd_today_count >= 5 THEN 0 ELSE 1 END
        FROM LatestTower lt
        JOIN tbl_m_devices d ON lt.device_id = d.id
        JOIN tbl_m_zones   z ON d.zone_id    = z.id
        JOIN tbl_m_sites   s ON z.site_id    = s.id
        JOIN TodaySpd      ts ON ts.device_id = lt.device_id
        WHERE lt.rn = 1
          AND d.is_active = 1
          AND (@SiteID IS NULL OR s.id = @SiteID)
          AND ts.spd_today_count >= 1

        UNION ALL

        -- ── Branch 5: CURAH HUJAN ──────────────────────────────────
        SELECT
            'environmental',
            z.id,
            s.site_code,
            d.device_name,
            CASE WHEN lt.rainfall > 50 THEN 'critical' ELSE 'warning' END,
            'weather',
            CASE
                WHEN lt.rainfall > 50
                THEN 'Hujan Sangat Lebat (>' + CAST(CAST(lt.rainfall AS INT) AS NVARCHAR(10)) + ' mm/jam)'
                ELSE 'Hujan Lebat (>'          + CAST(CAST(lt.rainfall AS INT) AS NVARCHAR(10)) + ' mm/jam)'
            END,
            NULL, NULL, NULL, NULL, NULL,
            CASE WHEN lt.rainfall > 50 THEN 0 ELSE 1 END
        FROM LatestTower lt
        JOIN tbl_m_devices d ON lt.device_id = d.id
        JOIN tbl_m_zones   z ON d.zone_id    = z.id
        JOIN tbl_m_sites   s ON z.site_id    = s.id
        WHERE lt.rn = 1
          AND d.is_active = 1
          AND (@SiteID IS NULL OR s.id = @SiteID)
          AND lt.rainfall IS NOT NULL
          AND lt.rainfall > 30

        UNION ALL

        -- ── Branch 6: KUALITAS UDARA & DEBU JALAN HAULING (BARU!) ───
        SELECT
            'environmental',
            z.id,
            s.site_code,
            d.device_name,
            CASE WHEN lt.air_quality = 'Hazardous' THEN 'critical' ELSE 'warning' END,
            'weather', -- Masuk kategori environment/weather untuk icon UI
            CASE 
                WHEN lt.air_quality = 'Hazardous' 
                THEN 'Debu Ekstrem / Hazardous (PM10: ' + CAST(CAST(ROUND(ISNULL(lt.pm10, 0),0) AS INT) AS NVARCHAR(10)) + ')'
                ELSE 'Debu Tinggi / Berpotensi Blind Spot (PM10: ' + CAST(CAST(ROUND(ISNULL(lt.pm10, 0),0) AS INT) AS NVARCHAR(10)) + ')'
            END,
            NULL, NULL, NULL, NULL, NULL,
            CASE WHEN lt.air_quality = 'Hazardous' THEN 0 ELSE 1 END
        FROM LatestTower lt
        JOIN tbl_m_devices d ON lt.device_id = d.id
        JOIN tbl_m_zones   z ON d.zone_id    = z.id
        JOIN tbl_m_sites   s ON z.site_id    = s.id
        WHERE lt.rn = 1
          AND d.is_active = 1
          AND (@SiteID IS NULL OR s.id = @SiteID)
          -- Trigger anomali jika level debu mencapai batas peringatan
          AND lt.air_quality IN ('Hazardous', 'Dusty')
    )

    SELECT
        panel,
        zone_id,
        site,
        name,
        anomaly_status      AS status,
        anomaly_type        AS type,
        anomaly_value       AS value,
        meta_voltage,
        meta_voltage_status,
        meta_frequency,
        meta_freq_status,
        tilt_value
    FROM AllAnomalies
    ORDER BY
        panel       ASC,
        sort_order  ASC,
        name        ASC;

END
GO

-- =============================================
-- UPDATE SP Get Dashboard Master Summary
-- Update: Hapus AreaID & Join Area, Fokus pada hierarki Site -> Zone
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetDashboardMasterSummary]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetDashboardMasterSummary];
GO
CREATE PROCEDURE [dbo].[sp_GetDashboardMasterSummary]
    @SiteID UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. Identifikasi ID Asset Type
    DECLARE @TowerTypeID UNIQUEIDENTIFIER = (SELECT id FROM tbl_r_asset_types WHERE type_name = 'TOWER');
    DECLARE @GensetTypeID UNIQUEIDENTIFIER = (SELECT id FROM tbl_r_asset_types WHERE type_name = 'GENSET');

    -- 2. Tabel bantu untuk mengecek kondisi per Zona
    DECLARE @ZoneStatus TABLE (
        zone_id UNIQUEIDENTIFIER,
        is_tower_active BIT,
        is_genset_active BIT
    );

    -- Ambil status terakhir tower dan genset di setiap zona
    INSERT INTO @ZoneStatus (zone_id, is_tower_active, is_genset_active)
    SELECT 
        z.id,
        MAX(CASE WHEN d.asset_type_id = @TowerTypeID THEN CAST(d.is_active AS INT) ELSE 0 END),
        MAX(CASE WHEN d.asset_type_id = @GensetTypeID THEN CAST(d.is_active AS INT) ELSE 0 END)
    FROM tbl_m_zones z
    INNER JOIN tbl_m_sites s ON z.site_id = s.id
    LEFT JOIN tbl_m_devices d ON d.zone_id = z.id
    WHERE (@SiteID IS NULL OR s.id = @SiteID)
    GROUP BY z.id;

    -- 3. Hitung Statistik berdasarkan logika sistem gabungan
    DECLARE @TowerUp    INT = 0,
            @TowerDown  INT = 0,
            @TowerTotal INT = 0;

    -- BTS Tower UP: Jika salah satu saja aktif (Tower Aktif ATAU Genset Aktif)
    SELECT @TowerUp = COUNT(*) 
    FROM @ZoneStatus 
    WHERE is_tower_active = 1 OR is_genset_active = 1;

    -- BTS Tower DOWN: Jika dua-duanya mati (Tower Mati DAN Genset Mati)
    SELECT @TowerDown = COUNT(*) 
    FROM @ZoneStatus 
    WHERE is_tower_active = 0 AND is_genset_active = 0;

    -- Total Zona
    SELECT @TowerTotal = COUNT(*) FROM @ZoneStatus;

    -- 4. Data Tambahan untuk Generator Panel & Detail
    DECLARE @PhysGenDown INT, @GenTotal INT, @GenReady INT;
    
    SELECT 
        @GenTotal = COUNT(d.id),
        @PhysGenDown = SUM(CASE WHEN d.is_active = 0 THEN 1 ELSE 0 END),
        @GenReady = SUM(CASE WHEN d.is_active = 1 THEN 1 ELSE 0 END)
    FROM tbl_m_devices d 
    JOIN tbl_m_zones z ON d.zone_id = z.id 
    JOIN tbl_m_sites s ON z.site_id = s.id
    WHERE d.asset_type_id = @GensetTypeID
      AND (@SiteID IS NULL OR s.id = @SiteID);

    -- 5. Perhitungan Nilai Rata-rata Sensor (DIPERBARUI DENGAN ETL)
    DECLARE @AvgFuel DECIMAL(18,2), @AvgTilt DECIMAL(18,2), @AvgWind DECIMAL(18,2), @AvgTemp DECIMAL(18,2);
    -- Variabel Baru untuk Ekstraksi ETL
    DECLARE @AvgDeviation DECIMAL(18,4), @AvgPM10 DECIMAL(18,2); 

    SELECT @AvgFuel = AVG(fuel_level) 
    FROM (SELECT TOP 100 fuel_level FROM tbl_t_genset_logs g JOIN tbl_m_devices dev ON g.device_id = dev.id JOIN tbl_m_zones zn ON dev.zone_id = zn.id WHERE (@SiteID IS NULL OR zn.site_id = @SiteID) ORDER BY g.device_time DESC) AS sub;

    -- Menambahkan penarikan data deviation_m dari tabel log tower
    SELECT 
        @AvgTilt = AVG(ABS(tilt_x) + ABS(tilt_y))/2,
        @AvgDeviation = AVG(deviation_m) 
    FROM (SELECT TOP 100 tilt_x, tilt_y, deviation_m FROM tbl_t_tower_logs t JOIN tbl_m_devices dev ON t.device_id = dev.id JOIN tbl_m_zones zn ON dev.zone_id = zn.id WHERE (@SiteID IS NULL OR zn.site_id = @SiteID) ORDER BY t.device_time DESC) AS sub;

    -- Menambahkan penarikan rata-rata pm10 untuk kualitas udara jalan hauling
    SELECT 
        @AvgWind = AVG(wind_speed), 
        @AvgTemp = AVG(temperature),
        @AvgPM10 = AVG(pm10)
    FROM (SELECT TOP 100 wind_speed, temperature, pm10 FROM tbl_t_tower_logs t JOIN tbl_m_devices dev ON t.device_id = dev.id JOIN tbl_m_zones zn ON dev.zone_id = zn.id WHERE (@SiteID IS NULL OR zn.site_id = @SiteID) ORDER BY t.device_time DESC) AS sub;

    -- 6. OUTPUT FINAL
    SELECT
        @TowerUp      AS tower_up,          -- Sistem dianggap hidup (Salah satu/keduanya ON)
        @TowerDown    AS tower_down,        -- Sistem dianggap mati (Keduanya OFF)
        @TowerTotal   AS tower_total,       
        @PhysGenDown  AS generator_down,    -- Fisik genset yang mati
        @TowerDown    AS bts_tower_down,    -- Sinkron dengan tower_down
        @GenTotal     AS gen_total,
        @GenReady     AS gen_ready_count,
        CASE WHEN @GenTotal > 0 THEN CAST(@GenReady AS INT) * 100 / @GenTotal ELSE 0 END AS gen_ready_percent,
        ISNULL(@AvgFuel, 0) AS avg_fuel_level,
        ISNULL(@AvgTilt, 0) AS avg_tilt,
        ISNULL(@AvgWind, 0) AS avg_wind_speed,
        ISNULL(@AvgTemp, 0) AS avg_temperature,
        -- Mengirim parameter tambahan untuk UI / Reporting
        ISNULL(CAST(@AvgDeviation * 100 AS DECIMAL(18,2)), 0) AS avg_deviation_cm, 
        ISNULL(@AvgPM10, 0) AS avg_pm10;
END
GO
-- =============================================
-- SP Get Down Device List
-- Mengembalikan daftar device offline per tipe:
--   panel = 'tower' | 'generator' | 'bts'
-- Dipanggil oleh DashboardController::buildSummaryData()
-- =============================================
-- =============================================
-- PATCH: Tambah zone_id ke sp_GetDownDeviceList
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetDownDeviceList]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetDownDeviceList];
GO
CREATE PROCEDURE [dbo].[sp_GetDownDeviceList]
    @SiteID UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        'tower'             AS panel,
        z.id                AS zone_id,      -- Digunakan untuk navigasi klik ke zona spesifik
        s.site_code         AS site,         -- Kode Distrik (ASMI, KPCS, dll)
        d.device_name       AS name,
        s.site_name         AS site_full,
        'critical'          AS status,
        d.updated_at        AS last_seen
    FROM tbl_m_devices d
    JOIN tbl_m_zones z        ON d.zone_id = z.id
    JOIN tbl_m_sites s        ON z.site_id = s.id
    JOIN tbl_r_asset_types at ON d.asset_type_id = at.id
    WHERE at.type_name = 'TOWER' 
      AND d.is_active = 0
      AND (@SiteID IS NULL OR s.id = @SiteID)

    UNION ALL

    SELECT
        'generator', 
        z.id, 
        s.site_code, 
        d.device_name, 
        s.site_name, 
        'critical', 
        d.updated_at
    FROM tbl_m_devices d
    JOIN tbl_m_zones z        ON d.zone_id = z.id 
    JOIN tbl_m_sites s        ON z.site_id = s.id 
    JOIN tbl_r_asset_types at ON d.asset_type_id = at.id
    WHERE at.type_name = 'GENSET' 
      AND d.is_active = 0
      AND (@SiteID IS NULL OR s.id = @SiteID)

    UNION ALL

    SELECT
        'bts', 
                z.id,
                s.site_code,
                z.zone_name,
                s.site_name,
                'critical',
                (
                        SELECT MAX(d2.updated_at)
                        FROM tbl_m_devices d2
                        JOIN tbl_r_asset_types at2 ON d2.asset_type_id = at2.id
                        WHERE d2.zone_id = z.id
                            AND UPPER(at2.type_name) IN ('TOWER', 'GENSET')
                            AND d2.is_active = 0
                ) AS last_seen
        FROM tbl_m_zones z
        JOIN tbl_m_sites s ON z.site_id = s.id
        WHERE EXISTS (
                        SELECT 1
                        FROM tbl_m_devices dt
                        JOIN tbl_r_asset_types att ON dt.asset_type_id = att.id
                        WHERE dt.zone_id = z.id
                            AND UPPER(att.type_name) = 'TOWER'
                            AND dt.is_active = 0
                )
            AND EXISTS (
                        SELECT 1
                        FROM tbl_m_devices dg
                        JOIN tbl_r_asset_types atg ON dg.asset_type_id = atg.id
                        WHERE dg.zone_id = z.id
                            AND UPPER(atg.type_name) = 'GENSET'
                            AND dg.is_active = 0
                )
      AND (@SiteID IS NULL OR s.id = @SiteID)
      
    ORDER BY panel, site, name;
END
GO

-- =============================================
-- SP Get Zone Info (for deep-link resolve)
-- Return: zone_id, site_id, zone_name
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetZoneInfo]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetZoneInfo];
GO
CREATE PROCEDURE [dbo].[sp_GetZoneInfo]
    @ZoneID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        z.id AS zone_id,
        z.site_id,
        z.zone_name
    FROM tbl_m_zones z
    WHERE z.id = @ZoneID;
END;
GO

-- =============================================
-- SP Get Zone Sensor Snapshots
-- Return latest merged sensor snapshot per zone
-- @SiteID = NULL => semua site
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetZoneSensorSnapshots]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetZoneSensorSnapshots];
GO
CREATE PROCEDURE [dbo].[sp_GetZoneSensorSnapshots]
    @SiteID UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TowerTypeID UNIQUEIDENTIFIER = (SELECT TOP 1 id FROM tbl_r_asset_types WHERE UPPER(type_name) = 'TOWER');
    DECLARE @GensetTypeID UNIQUEIDENTIFIER = (SELECT TOP 1 id FROM tbl_r_asset_types WHERE UPPER(type_name) = 'GENSET');

    SELECT
        z.site_id,
        z.id AS zone_id,
        s.site_code,
        s.site_name,
        z.zone_name,
        tower_dev.device_name AS tower_device_name,
        gen_dev.device_name AS generator_device_name,
        g.fuel_level,
        g.power_factor,
        g.energy_kwh,
        t.wind_speed,
        t.spd_count,
        t.ground_resistance,
        t.tilt_x,
        t.tilt_y,
        t.illuminance AS luminosity,
        t.illuminance AS lux,
        t.rainfall AS precipitation,
        t.pm2_5,
        t.pm10 AS pm_10,
        t.pm10 AS dust_level,
        t.air_quality,
        t.deviation_m,
        tower_dev.asset_height
    FROM tbl_m_zones z
        INNER JOIN tbl_m_sites s ON s.id = z.site_id
    OUTER APPLY (
                SELECT TOP 1 d.asset_height, d.device_name
        FROM tbl_m_devices d
                WHERE d.zone_id = z.id
                    AND d.asset_type_id = @TowerTypeID
        ORDER BY d.is_active DESC, d.created_at ASC
    ) tower_dev
        OUTER APPLY (
                SELECT TOP 1 d.device_name
                FROM tbl_m_devices d
                WHERE d.zone_id = z.id
                    AND d.asset_type_id = @GensetTypeID
                ORDER BY d.is_active DESC, d.created_at ASC
        ) gen_dev
    OUTER APPLY (
                SELECT TOP 1 gl.fuel_level, gl.power_factor, gl.energy_kwh
        FROM tbl_t_genset_logs gl
        INNER JOIN tbl_m_devices d ON d.id = gl.device_id
        WHERE d.zone_id = z.id
        ORDER BY gl.device_time DESC, gl.created_at DESC
    ) g
    OUTER APPLY (
        SELECT TOP 1
            tl.wind_speed,
            tl.spd_count,
            tl.ground_resistance,
            tl.tilt_x,
            tl.tilt_y,
            tl.illuminance,
            tl.rainfall,
            tl.pm2_5,
            tl.pm10,
            tl.air_quality,
            tl.deviation_m
        FROM tbl_t_tower_logs tl
        INNER JOIN tbl_m_devices d ON d.id = tl.device_id
        WHERE d.zone_id = z.id
        ORDER BY tl.device_time DESC, tl.created_at DESC
    ) t
    WHERE (@SiteID IS NULL OR z.site_id = @SiteID);
END;
GO

-- =============================================
-- SP Is User Administrator
-- Return: is_admin (bit)
-- =============================================
IF OBJECT_ID('[dbo].[sp_IsUserAdministrator]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_IsUserAdministrator];
GO
CREATE PROCEDURE [dbo].[sp_IsUserAdministrator]
    @UserID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CASE
            WHEN EXISTS (
                SELECT 1
                FROM tbl_m_users u
                INNER JOIN tbl_r_roles r ON r.id = u.role_id
                WHERE u.id = @UserID
                  AND r.role_name = 'Administrator'
            ) THEN CAST(1 AS BIT)
            ELSE CAST(0 AS BIT)
        END AS is_admin;
END;
GO

-- =============================================
-- SP Get User Assigned Site IDs
-- Return: site_id list
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetUserAssignedSites]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetUserAssignedSites];
GO
CREATE PROCEDURE [dbo].[sp_GetUserAssignedSites]
    @UserID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ua.site_id
    FROM tbl_r_user_assignments ua
    WHERE ua.user_id = @UserID;
END;
GO

-- =============================================
-- SP Get Dashboard Fallback Metrics
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetDashboardFallbackMetrics]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetDashboardFallbackMetrics];
GO
CREATE PROCEDURE [dbo].[sp_GetDashboardFallbackMetrics]
    @SiteID UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TowerTypeID UNIQUEIDENTIFIER = (SELECT TOP 1 id FROM tbl_r_asset_types WHERE UPPER(type_name) = 'TOWER');
    DECLARE @GensetTypeID UNIQUEIDENTIFIER = (SELECT TOP 1 id FROM tbl_r_asset_types WHERE UPPER(type_name) = 'GENSET');

    ;WITH filtered_genset AS (
        SELECT d.id, d.is_active
        FROM tbl_m_devices d
        INNER JOIN tbl_m_zones z ON z.id = d.zone_id
        INNER JOIN tbl_m_sites s ON s.id = z.site_id
        WHERE d.asset_type_id = @GensetTypeID
          AND (@SiteID IS NULL OR s.id = @SiteID)
    ),
    latest_genset AS (
        SELECT gl.device_id, gl.fuel_level,
               ROW_NUMBER() OVER (PARTITION BY gl.device_id ORDER BY gl.device_time DESC) AS rn
        FROM tbl_t_genset_logs gl
        INNER JOIN filtered_genset fg ON fg.id = gl.device_id
    ),
    filtered_tower AS (
        SELECT d.id
        FROM tbl_m_devices d
        INNER JOIN tbl_m_zones z ON z.id = d.zone_id
        INNER JOIN tbl_m_sites s ON s.id = z.site_id
        WHERE d.asset_type_id = @TowerTypeID
          AND (@SiteID IS NULL OR s.id = @SiteID)
    ),
    latest_tower AS (
        SELECT tl.device_id, tl.tilt_x, tl.tilt_y, tl.wind_speed, tl.temperature,
               ROW_NUMBER() OVER (PARTITION BY tl.device_id ORDER BY tl.device_time DESC) AS rn
        FROM tbl_t_tower_logs tl
        INNER JOIN filtered_tower ft ON ft.id = tl.device_id
    )
    SELECT
        (SELECT COUNT(*) FROM filtered_genset) AS gen_total,
        (SELECT COUNT(*) FROM filtered_genset WHERE is_active = 1) AS gen_ready,
        ISNULL((SELECT AVG(CAST(fuel_level AS FLOAT)) FROM latest_genset WHERE rn = 1), 0) AS avg_fuel,
        ISNULL((SELECT AVG((ABS(CAST(tilt_x AS FLOAT)) + ABS(CAST(tilt_y AS FLOAT))) / 2.0) FROM latest_tower WHERE rn = 1), 0) AS avg_tilt,
        ISNULL((SELECT AVG(CAST(wind_speed AS FLOAT)) FROM latest_tower WHERE rn = 1), 0) AS avg_wind,
        ISNULL((SELECT AVG(CAST(temperature AS FLOAT)) FROM latest_tower WHERE rn = 1), 0) AS avg_temp;
END;
GO

-- =============================================
-- SP Is Anomaly Acknowledged
-- =============================================
IF OBJECT_ID('[dbo].[sp_IsAnomalyAcknowledged]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_IsAnomalyAcknowledged];
GO
CREATE PROCEDURE [dbo].[sp_IsAnomalyAcknowledged]
    @ZoneID UNIQUEIDENTIFIER,
    @Panel NVARCHAR(50),
    @AnomalyType NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1 CAST(ISNULL(a.is_acknowledged, 0) AS INT) AS is_ack
    FROM tbl_t_anomaly_acknowledges a
    WHERE a.zone_id = @ZoneID
      AND LOWER(a.panel) = LOWER(@Panel)
      AND LOWER(a.anomaly_type) = LOWER(@AnomalyType)
      AND CAST(ISNULL(a.is_acknowledged, 0) AS INT) = 1
    ORDER BY a.acknowledged_at DESC, a.created_at DESC;
END;
GO

-- =============================================
-- SP Upsert Dashboard Anomaly Acknowledge
-- =============================================
IF OBJECT_ID('[dbo].[sp_DashboardUpsertAnomalyAcknowledge]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_DashboardUpsertAnomalyAcknowledge];
GO
CREATE PROCEDURE [dbo].[sp_DashboardUpsertAnomalyAcknowledge]
    @ZoneID UNIQUEIDENTIFIER,
    @Panel NVARCHAR(50),
    @AnomalyType NVARCHAR(100),
    @UserID UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM tbl_t_anomaly_acknowledges
        WHERE zone_id = @ZoneID
          AND LOWER(panel) = LOWER(@Panel)
          AND LOWER(anomaly_type) = LOWER(@AnomalyType)
    )
    BEGIN
        UPDATE tbl_t_anomaly_acknowledges
        SET is_acknowledged = 1,
            acknowledged_at = GETDATE(),
            acknowledged_by = @UserID,
            updated_at      = GETDATE()
        WHERE zone_id = @ZoneID
          AND LOWER(panel) = LOWER(@Panel)
          AND LOWER(anomaly_type) = LOWER(@AnomalyType);
    END
    ELSE
    BEGIN
        INSERT INTO tbl_t_anomaly_acknowledges (
            id,
            zone_id,
            panel,
            anomaly_type,
            is_acknowledged,
            acknowledged_at,
            acknowledged_by,
            created_at,
            updated_at
        )
        VALUES (
            NEWID(),
            @ZoneID,
            LOWER(@Panel),
            LOWER(@AnomalyType),
            1,
            GETDATE(),
            @UserID,
            GETDATE(),
            GETDATE()
        );
    END
END;
GO

-- =============================================
-- SP Release Resolved Acknowledges
-- @ActiveScopesCsv format: zone|panel|type,zone|panel|type
-- =============================================
IF OBJECT_ID('[dbo].[sp_DashboardReleaseResolvedAcknowledges]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_DashboardReleaseResolvedAcknowledges];
GO
CREATE PROCEDURE [dbo].[sp_DashboardReleaseResolvedAcknowledges]
    @SiteID UNIQUEIDENTIFIER = NULL,
    @ActiveScopesCsv NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE a
    SET a.is_acknowledged = 0,
        a.updated_at = GETDATE()
    FROM tbl_t_anomaly_acknowledges a
    INNER JOIN tbl_m_zones z ON z.id = a.zone_id
    WHERE a.is_acknowledged = 1
      AND a.acknowledged_at IS NOT NULL
      AND a.acknowledged_at < DATEADD(MINUTE, -15, GETDATE())
      AND (@SiteID IS NULL OR z.site_id = @SiteID);

    IF @ActiveScopesCsv IS NULL OR LTRIM(RTRIM(@ActiveScopesCsv)) = ''
    BEGIN
        UPDATE a
        SET a.is_acknowledged = 0,
            a.updated_at = GETDATE()
        FROM tbl_t_anomaly_acknowledges a
        INNER JOIN tbl_m_zones z ON z.id = a.zone_id
        WHERE a.is_acknowledged = 1
          AND (@SiteID IS NULL OR z.site_id = @SiteID);
        RETURN;
    END

    ;WITH ActiveScopes AS (
        SELECT LOWER(LTRIM(RTRIM(value))) AS scope_key
        FROM STRING_SPLIT(@ActiveScopesCsv, ',')
        WHERE LTRIM(RTRIM(value)) <> ''
    )
    UPDATE a
    SET a.is_acknowledged = 0,
        a.updated_at = GETDATE()
    FROM tbl_t_anomaly_acknowledges a
    INNER JOIN tbl_m_zones z ON z.id = a.zone_id
    LEFT JOIN ActiveScopes s
        ON s.scope_key = LOWER(CONVERT(NVARCHAR(36), a.zone_id)) + '|' + LOWER(a.panel) + '|' + LOWER(a.anomaly_type)
    WHERE a.is_acknowledged = 1
      AND (@SiteID IS NULL OR z.site_id = @SiteID)
      AND s.scope_key IS NULL;
END;
GO

-- =============================================
-- SP Mark Latest Log Acknowledged (defensive)
-- Tidak gagal jika kolom ack belum ada di tabel log.
-- =============================================
IF OBJECT_ID('[dbo].[sp_DashboardMarkLatestLogAcknowledged]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_DashboardMarkLatestLogAcknowledged];
GO
CREATE PROCEDURE [dbo].[sp_DashboardMarkLatestLogAcknowledged]
    @ZoneID UNIQUEIDENTIFIER,
    @AssetType NVARCHAR(20),
    @UserID UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TargetTable SYSNAME;
    DECLARE @HasIsAck BIT;
    DECLARE @HasAckAt BIT;
    DECLARE @HasAckBy BIT;

    SET @TargetTable = CASE
        WHEN UPPER(@AssetType) = 'GENSET' THEN 'tbl_t_genset_logs'
        ELSE 'tbl_t_tower_logs'
    END;

    SET @HasIsAck = CASE
        WHEN EXISTS (
            SELECT 1
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @TargetTable
              AND COLUMN_NAME = 'is_acknowledged'
        ) THEN 1 ELSE 0 END;

    SET @HasAckAt = CASE
        WHEN EXISTS (
            SELECT 1
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @TargetTable
              AND COLUMN_NAME = 'acknowledged_at'
        ) THEN 1 ELSE 0 END;

    SET @HasAckBy = CASE
        WHEN EXISTS (
            SELECT 1
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @TargetTable
              AND COLUMN_NAME = 'acknowledged_by'
        ) THEN 1 ELSE 0 END;

    IF @HasIsAck = 0 OR @HasAckAt = 0 OR @HasAckBy = 0
    BEGIN
        SELECT CAST(0 AS INT) AS affected_rows;
        RETURN;
    END

    DECLARE @Sql NVARCHAR(MAX) = N'
        ;WITH latest_log AS (
            SELECT l.id,
                   ROW_NUMBER() OVER (PARTITION BY l.device_id ORDER BY l.device_time DESC, l.created_at DESC) AS rn
            FROM ' + QUOTENAME(@TargetTable) + N' l
            INNER JOIN tbl_m_devices d ON d.id = l.device_id
            INNER JOIN tbl_r_asset_types at ON at.id = d.asset_type_id
            WHERE d.zone_id = @pZoneID
              AND UPPER(at.type_name) = @pAsset
        )
        UPDATE target
        SET target.is_acknowledged = 1,
            target.acknowledged_at = GETDATE(),
            target.acknowledged_by = @pUserID
        FROM ' + QUOTENAME(@TargetTable) + N' target
        INNER JOIN latest_log ll ON ll.id = target.id
        WHERE ll.rn = 1;

        SELECT CAST(@@ROWCOUNT AS INT) AS affected_rows;
    ';

    DECLARE @AssetTypeUpper NVARCHAR(20);
    SET @AssetTypeUpper = UPPER(@AssetType);

    EXEC sp_executesql
        @Sql,
        N'@pZoneID UNIQUEIDENTIFIER, @pAsset NVARCHAR(20), @pUserID UNIQUEIDENTIFIER',
        @pZoneID = @ZoneID,
        @pAsset = @AssetTypeUpper,
        @pUserID = @UserID;
END;
GO

IF OBJECT_ID('[dbo].[sp_ExportLogsRaw]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_ExportLogsRaw];
GO
CREATE PROCEDURE [dbo].[sp_ExportLogsRaw]
    @SiteID UNIQUEIDENTIFIER = NULL,
    @DateFrom DATE,
    @DateTo DATE,
    @Offset INT = 0,
    @PageSize INT = 50000
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDT DATETIME = CAST(@DateFrom AS DATETIME);
    DECLARE @ToDT   DATETIME = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@DateTo AS DATETIME)));

    DECLARE @TowerTypeID  UNIQUEIDENTIFIER = (SELECT id FROM tbl_r_asset_types WHERE type_name = 'TOWER');
    DECLARE @GensetTypeID UNIQUEIDENTIFIER = (SELECT id FROM tbl_r_asset_types WHERE type_name = 'GENSET');

    -- ─── GENSET LOGS ─────────────────────────────────────────────────────────
    SELECT
        'genset'                                    AS log_type,
        gl.device_time,
        s.site_code,
        z.zone_name,
        d.device_name,
        'Genset'                                    AS device_type,
        CAST(gl.voltage AS NVARCHAR(50))            AS voltage,
        CAST(gl.[current] AS NVARCHAR(50))          AS [current],
        CAST(gl.power_kw AS NVARCHAR(50))           AS power_kw,
        CAST(gl.energy_kwh AS NVARCHAR(50))         AS energy_kwh,
        CAST(gl.frequency AS NVARCHAR(50))          AS frequency,
        CAST(gl.power_factor AS NVARCHAR(50))       AS power_factor,
        CAST(gl.fuel_level AS NVARCHAR(50))         AS fuel_level,
        CAST(gl.illuminance AS NVARCHAR(50))        AS illuminance,
        NULL                                        AS wind_speed,
        NULL                                        AS wind_direction,
        NULL                                        AS temperature,
        NULL                                        AS humidity,
        NULL                                        AS pressure,
        NULL                                        AS rainfall,
        NULL                                        AS solar_radiation,
        NULL                                        AS pm2_5,
        NULL                                        AS pm10,
        NULL                                        AS tilt_x,
        NULL                                        AS tilt_y,
        NULL                                        AS ground_resistance,
        NULL                                        AS spd_count,
        NULL                                        AS lightning_current,
        NULL                                        AS deviation_m,
        NULL                                        AS weather_status,
        NULL                                        AS air_quality,
        NULL                                        AS wind_cardinal,
        CAST(gl.latitude AS NVARCHAR(50))           AS latitude,
        CAST(gl.longitude AS NVARCHAR(50))          AS longitude,
        gl.created_at,
        CAST(gl.created_by AS NVARCHAR(50))         AS created_by
    FROM tbl_t_genset_logs gl
    JOIN tbl_m_devices d ON d.id = gl.device_id
    JOIN tbl_m_zones z ON z.id = d.zone_id
    JOIN tbl_m_sites s ON s.id = z.site_id
    WHERE gl.device_time BETWEEN @FromDT AND @ToDT
      AND d.asset_type_id = @GensetTypeID
      AND (@SiteID IS NULL OR s.id = @SiteID)

    UNION ALL

    -- ─── TOWER LOGS ──────────────────────────────────────────────────────────
    SELECT
        'tower'                                     AS log_type,
        tl.device_time,
        s.site_code,
        z.zone_name,
        d.device_name,
        'Tower'                                     AS device_type,
        NULL                                        AS voltage,
        NULL                                        AS [current],
        NULL                                        AS power_kw,
        NULL                                        AS energy_kwh,
        NULL                                        AS frequency,
        NULL                                        AS power_factor,
        NULL                                        AS fuel_level,
        NULL                                        AS illuminance,
        CAST(tl.wind_speed AS NVARCHAR(50))         AS wind_speed,
        CAST(tl.wind_direction AS NVARCHAR(50))     AS wind_direction,
        CAST(tl.temperature AS NVARCHAR(50))        AS temperature,
        CAST(tl.humidity AS NVARCHAR(50))           AS humidity,
        CAST(tl.pressure AS NVARCHAR(50))           AS pressure,
        CAST(tl.rainfall AS NVARCHAR(50))           AS rainfall,
        CAST(tl.solar_radiation AS NVARCHAR(50))    AS solar_radiation,
        CAST(tl.pm2_5 AS NVARCHAR(50))              AS pm2_5,
        CAST(tl.pm10 AS NVARCHAR(50))               AS pm10,
        CAST(tl.tilt_x AS NVARCHAR(50))             AS tilt_x,
        CAST(tl.tilt_y AS NVARCHAR(50))             AS tilt_y,
        CAST(tl.ground_resistance AS NVARCHAR(50))  AS ground_resistance,
        CAST(tl.spd_count AS NVARCHAR(50))          AS spd_count,
        CAST(tl.lightning_current AS NVARCHAR(50))  AS lightning_current,
        CAST(tl.deviation_m AS NVARCHAR(50))        AS deviation_m,
        CAST(tl.weather_status AS NVARCHAR(50))     AS weather_status,
        CAST(tl.air_quality AS NVARCHAR(50))        AS air_quality,
        CAST(tl.wind_cardinal AS NVARCHAR(50))      AS wind_cardinal,
        CAST(tl.latitude AS NVARCHAR(50))           AS latitude,
        CAST(tl.longitude AS NVARCHAR(50))          AS longitude,
        tl.created_at,
        CAST(tl.created_by AS NVARCHAR(50))         AS created_by
    FROM tbl_t_tower_logs tl
    JOIN tbl_m_devices d ON d.id = tl.device_id
    JOIN tbl_m_zones z ON z.id = d.zone_id
    JOIN tbl_m_sites s ON s.id = z.site_id
    WHERE tl.device_time BETWEEN @FromDT AND @ToDT
      AND d.asset_type_id = @TowerTypeID
      AND (@SiteID IS NULL OR s.id = @SiteID)

    ORDER BY device_time ASC, log_type ASC, site_code ASC, device_name ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END;
GO

GO

-- === 06_tracking_stored_procedures.sql ===

GO

-- =============================================
-- TRACKING STORED PROCEDURES
-- Untuk Fleet Tracking System
-- =============================================

-- =============================================
-- 1. sp_GetTrackingSitesWithUnits
-- Get semua sites dengan units terbaru (current position)
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetTrackingSitesWithUnits]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetTrackingSitesWithUnits];
GO
CREATE PROCEDURE [dbo].[sp_GetTrackingSitesWithUnits]
    @UserID UNIQUEIDENTIFIER = NULL,
    @OnlyActiveSites BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IsAdmin BIT = 0;

    -- Check if user is admin
    IF @UserID IS NOT NULL
    BEGIN
        SELECT @IsAdmin = CASE 
            WHEN COUNT(*) > 0 THEN 1 
            ELSE 0 
        END
        FROM tbl_m_users u
        JOIN tbl_r_roles r ON r.id = u.role_id
        WHERE u.id = @UserID 
          AND r.role_name = 'Administrator';
    END

    -- Get tracking devices (asset_type = 'Tracking') ONLY - filter out UPS, Genset, Tower
    -- Only latest position dari fleet_tracking_logs
    SELECT 
        s.id AS site_id,
        s.site_code,
        s.site_name,
        s.latitude AS site_latitude,
        s.longitude AS site_longitude,
        d.id AS device_id,
        d.serial_number,
        d.device_name,
        d.fleet_type,
        ISNULL(d.is_active, 1) AS is_active,
        ISNULL(ltp.latitude, s.latitude) AS current_latitude,
        ISNULL(ltp.longitude, s.longitude) AS current_longitude,
        ISNULL(ltp.speed_kmh, 0) AS speed_kmh,
        ISNULL(ltp.heading_degree, 0) AS heading_degree,
        ISNULL(ltp.gps_status, 'Unknown') AS gps_status,
        ISNULL(ltp.satellite_in_use, 0) AS satellite_in_use,
        ISNULL(ltp.altitude, 0) AS altitude,
        ISNULL(ltp.network_rssi, NULL) AS network_rssi,
        ISNULL(ltp.network_rsrp, NULL) AS network_rsrp,
        ISNULL(ltp.network_rsrq, NULL) AS network_rsrq,
        ISNULL(ltp.network_sinr, NULL) AS network_sinr,
        ISNULL(ltp.device_time, GETUTCDATE()) AS last_update,
        CASE
             WHEN ltp.device_time IS NULL THEN 'off'
             WHEN (ltp.device_time >= DATEADD(MINUTE, -15, GETDATE()) OR ltp.device_time >= DATEADD(MINUTE, -15, GETUTCDATE())) AND ISNULL(ltp.speed_kmh, 0) > 2 THEN 'on'
             WHEN (ltp.device_time >= DATEADD(HOUR, -2, GETDATE()) OR ltp.device_time >= DATEADD(HOUR, -2, GETUTCDATE())) THEN 'idle'
             ELSE 'off'
        END AS status
    FROM tbl_m_sites s
    LEFT JOIN tbl_m_devices d ON (d.site_id = s.id OR (d.zone_id IS NOT NULL AND EXISTS(
        SELECT 1 FROM tbl_m_zones z WHERE z.id = d.zone_id AND z.site_id = s.id
    )))
    LEFT JOIN tbl_r_asset_types at ON at.id = d.asset_type_id
    OUTER APPLY (
        SELECT TOP 1
            ftl.latitude,
            ftl.longitude,
            ftl.speed_kmh,
            ftl.heading_degree,
            ftl.gps_status,
            ftl.satellite_in_use,
            ftl.altitude,
            ftl.network_rssi,
            ftl.network_rsrp,
            ftl.network_rsrq,
            ftl.network_sinr,
            ftl.device_time
        FROM tbl_t_fleet_tracking_logs ftl
        WHERE ftl.device_id = d.id
          AND ftl.device_time >= DATEADD(DAY, -1, GETUTCDATE())
          AND ftl.latitude IS NOT NULL
          AND ftl.longitude IS NOT NULL
        ORDER BY ftl.device_time DESC
    ) ltp
    WHERE 1=1
        AND (@IsAdmin = 1 OR @UserID IS NULL)  -- For now, only admin can see (add user assignment later)
        AND d.asset_type_id IN (SELECT id FROM tbl_r_asset_types WHERE type_name = 'Tracking')  -- CRITICAL: Only Tracking
        AND (@OnlyActiveSites = 0 OR s.site_code IN (
            SELECT DISTINCT CASE 
                WHEN d2.site_id IS NOT NULL THEN s2.site_code
                WHEN z.site_id IS NOT NULL THEN s3.site_code
            END
            FROM tbl_m_devices d2
            LEFT JOIN tbl_m_zones z ON z.id = d2.zone_id
            LEFT JOIN tbl_m_sites s3 ON s3.id = z.site_id
            LEFT JOIN tbl_m_sites s2 ON s2.id = d2.site_id
            LEFT JOIN tbl_r_asset_types at2 ON at2.id = d2.asset_type_id
            WHERE d2.is_active = 1
              AND at2.type_name = 'Tracking'  -- CRITICAL: Only Tracking
        ))
    ORDER BY s.site_code, d.device_name;
END;
GO


-- =============================================
-- 2. sp_GetTrackingHistory
-- Get track history untuk satu device dalam date range
-- FIXED: Haversine formula with safety bounds on ACOS
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetTrackingHistory]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetTrackingHistory];
GO
CREATE PROCEDURE [dbo].[sp_GetTrackingHistory]
    @DeviceID UNIQUEIDENTIFIER,
    @DateFrom DATE,
    @DateTo DATE = NULL,
    @TimeFrom TIME = '00:00:00',
    @TimeTo TIME = '23:59:59'
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDT DATETIME = CAST(CAST(@DateFrom AS DATETIME) + CAST(@TimeFrom AS DATETIME) AS DATETIME);
    DECLARE @ToDT DATETIME = CAST(
        CAST(ISNULL(@DateTo, @DateFrom) AS DATETIME) 
        + CAST(@TimeTo AS DATETIME) AS DATETIME
    );

    -- Get track points with proper LAG calculation
    ;WITH TrackWithLag AS (
        SELECT 
            id,
            device_id,
            latitude,
            longitude,
            speed_kmh,
            heading_degree,
            altitude,
            gps_status,
            satellite_in_use,
            network_rssi,
            network_rsrp,
            network_rsrq,
            network_sinr,
            device_time,
            created_at,
            LAG(latitude) OVER (ORDER BY device_time) AS prev_latitude,
            LAG(longitude) OVER (ORDER BY device_time) AS prev_longitude
        FROM tbl_t_fleet_tracking_logs
        WHERE device_id = @DeviceID
            AND device_time BETWEEN @FromDT AND @ToDT
            AND latitude IS NOT NULL
            AND longitude IS NOT NULL
            AND gps_status != 'Not Fixed'
            AND latitude != 0.0
            AND longitude != 0.0
    ),
    DistanceCalc AS (
        SELECT 
            id,
            device_id,
            latitude,
            longitude,
            speed_kmh,
            heading_degree,
            altitude,
            gps_status,
            satellite_in_use,
            network_rssi,
            network_rsrp,
            network_rsrq,
            network_sinr,
            device_time,
            created_at,
            -- Haversine distance calculation with ACOS safety bounds
            CASE 
                WHEN prev_latitude IS NOT NULL AND prev_longitude IS NOT NULL
                THEN (
                    111111 * DEGREES(
                        ACOS(
                            CASE 
                                -- Clamp value to [-1, 1] to avoid ACOS domain error
                                WHEN (SIN(RADIANS(latitude)) * SIN(RADIANS(prev_latitude))
                                    + COS(RADIANS(latitude)) * COS(RADIANS(prev_latitude))
                                    * COS(RADIANS(longitude - prev_longitude))) > 1
                                THEN 1
                                WHEN (SIN(RADIANS(latitude)) * SIN(RADIANS(prev_latitude))
                                    + COS(RADIANS(latitude)) * COS(RADIANS(prev_latitude))
                                    * COS(RADIANS(longitude - prev_longitude))) < -1
                                THEN -1
                                ELSE (SIN(RADIANS(latitude)) * SIN(RADIANS(prev_latitude))
                                    + COS(RADIANS(latitude)) * COS(RADIANS(prev_latitude))
                                    * COS(RADIANS(longitude - prev_longitude)))
                            END
                        )
                    )
                )
                ELSE 0 
            END AS distance_from_prev_m
        FROM TrackWithLag
    )
    SELECT 
        id,
        device_id,
        latitude,
        longitude,
        speed_kmh,
        heading_degree,
        altitude,
        gps_status,
        satellite_in_use,
        network_rssi,
        network_rsrp,
        network_rsrq,
        network_sinr,
        device_time,
        created_at,
        distance_from_prev_m
    FROM DistanceCalc
    ORDER BY device_time ASC;
END;
GO


-- =============================================
-- 3. sp_GetCurrentTrackingPosition
-- Get current position untuk satu device
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetCurrentTrackingPosition]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetCurrentTrackingPosition];
GO
CREATE PROCEDURE [dbo].[sp_GetCurrentTrackingPosition]
    @DeviceID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        ftl.id,
        ftl.device_id,
        d.serial_number,
        d.device_name,
        d.fleet_type,
        s.site_code,
        s.site_name,
        ftl.latitude,
        ftl.longitude,
        ftl.speed_kmh,
        ftl.heading_degree,
        ftl.altitude,
        ftl.gps_status,
        ftl.satellite_in_use,
        ftl.network_rssi,
        ftl.network_rsrp,
        ftl.network_rsrq,
        ftl.network_sinr,
        ftl.device_time,
        CASE
            WHEN ftl.device_time IS NULL THEN 'off'
            WHEN (ftl.device_time >= DATEADD(MINUTE, -15, GETDATE()) OR ftl.device_time >= DATEADD(MINUTE, -15, GETUTCDATE())) AND ISNULL(ftl.speed_kmh, 0) > 2 THEN 'on'
            WHEN (ftl.device_time >= DATEADD(HOUR, -2, GETDATE()) OR ftl.device_time >= DATEADD(HOUR, -2, GETUTCDATE())) THEN 'idle'
            ELSE 'off'
        END AS status
    FROM tbl_t_fleet_tracking_logs ftl
    INNER JOIN tbl_m_devices d ON d.id = ftl.device_id
    LEFT JOIN tbl_m_sites s ON s.id = d.site_id
    WHERE ftl.device_id = @DeviceID
    ORDER BY ftl.device_time DESC;
END;
GO


-- =============================================
-- 4. sp_GetTrackingSummary
-- Get summary statistics untuk satu device dalam date range
-- Includes: total distance, stops, idle time, speed stats
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetTrackingSummary]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetTrackingSummary];
GO
CREATE PROCEDURE [dbo].[sp_GetTrackingSummary]
    @DeviceID UNIQUEIDENTIFIER,
    @DateFrom DATE,
    @DateTo DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDT DATETIME = CAST(@DateFrom AS DATETIME);
    DECLARE @ToDT DATETIME = CAST(ISNULL(@DateTo, @DateFrom) AS DATETIME) + CAST('23:59:59' AS DATETIME);

    -- Calculate summary metrics
    ;WITH TrackData AS (
        SELECT 
            device_id,
            latitude,
            longitude,
            speed_kmh,
            device_time,
            LAG(latitude) OVER (ORDER BY device_time) AS prev_lat,
            LAG(longitude) OVER (ORDER BY device_time) AS prev_lng,
            LAG(device_time) OVER (ORDER BY device_time) AS prev_time,
            DATEDIFF(SECOND, LAG(device_time) OVER (ORDER BY device_time), device_time) AS time_diff_sec
        FROM tbl_t_fleet_tracking_logs
        WHERE device_id = @DeviceID
            AND device_time BETWEEN @FromDT AND @ToDT
            AND gps_status != 'Not Fixed'
            AND latitude != 0.0
            AND longitude != 0.0
    ),
    DistanceCalc AS (
        SELECT 
            device_id,
            speed_kmh,
            time_diff_sec,
            -- Haversine distance calculation with ACOS safety bounds
            CASE 
                WHEN prev_lat IS NOT NULL
                THEN (
                    111111 * DEGREES(
                        ACOS(
                            CASE 
                                -- Clamp value to [-1, 1] to avoid ACOS domain error
                                WHEN (SIN(RADIANS(latitude)) * SIN(RADIANS(prev_lat))
                                    + COS(RADIANS(latitude)) * COS(RADIANS(prev_lat))
                                    * COS(RADIANS(longitude - prev_lng))) > 1
                                THEN 1
                                WHEN (SIN(RADIANS(latitude)) * SIN(RADIANS(prev_lat))
                                    + COS(RADIANS(latitude)) * COS(RADIANS(prev_lat))
                                    * COS(RADIANS(longitude - prev_lng))) < -1
                                THEN -1
                                ELSE (SIN(RADIANS(latitude)) * SIN(RADIANS(prev_lat))
                                    + COS(RADIANS(latitude)) * COS(RADIANS(prev_lat))
                                    * COS(RADIANS(longitude - prev_lng)))
                            END
                        )
                    ) / 1000.0  -- Convert to km
                )
                ELSE 0 
            END AS distance_km,
            CASE 
                WHEN speed_kmh < 5 AND time_diff_sec > 300 THEN 1  -- Stop > 5 minutes
                ELSE 0 
            END AS is_stop
        FROM TrackData
    )
    SELECT 
        @DeviceID AS device_id,
        @DateFrom AS date_from,
        @DateTo AS date_to,
                (SELECT COUNT(*) FROM tbl_t_fleet_tracking_logs 
                 WHERE device_id = @DeviceID AND device_time BETWEEN @FromDT AND @ToDT
                     AND latitude IS NOT NULL AND longitude IS NOT NULL) AS total_points,
        DATEDIFF(SECOND, 
            (SELECT TOP 1 device_time FROM tbl_t_fleet_tracking_logs 
                         WHERE device_id = @DeviceID AND device_time BETWEEN @FromDT AND @ToDT 
                             AND latitude IS NOT NULL AND longitude IS NOT NULL
             ORDER BY device_time ASC),
            (SELECT TOP 1 device_time FROM tbl_t_fleet_tracking_logs 
                         WHERE device_id = @DeviceID AND device_time BETWEEN @FromDT AND @ToDT 
                             AND latitude IS NOT NULL AND longitude IS NOT NULL
             ORDER BY device_time DESC)
        ) AS total_duration,
        ISNULL(ROUND(SUM(distance_km), 2), 0) AS total_distance_km,
        ISNULL(COUNT(CASE WHEN is_stop = 1 THEN 1 END), 0) AS total_stops,
        ISNULL(SUM(CASE WHEN speed_kmh < 5 THEN time_diff_sec ELSE 0 END), 0) AS idle_time_seconds,
        ISNULL(ROUND(AVG(speed_kmh), 1), 0) AS avg_speed_kmh,
        ISNULL(MAX(speed_kmh), 0) AS max_speed_kmh,
        ISNULL(MIN(CASE WHEN speed_kmh > 0 THEN speed_kmh END), 0) AS min_speed_kmh
    FROM DistanceCalc;
END;
GO


-- =============================================
-- 5. sp_GetTrackingDailyStatistics
-- Get daily aggregated statistics untuk multiple devices
-- Useful untuk reports dan dashboards
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetTrackingDailyStatistics]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetTrackingDailyStatistics];
GO
CREATE PROCEDURE [dbo].[sp_GetTrackingDailyStatistics]
    @SiteID UNIQUEIDENTIFIER = NULL,
    @DateFrom DATE,
    @DateTo DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDT DATETIME = CAST(@DateFrom AS DATETIME);
    DECLARE @ToDT DATETIME = CAST(ISNULL(@DateTo, @DateFrom) AS DATETIME) + CAST('23:59:59' AS DATETIME);

    ;WITH TrackData AS (
        SELECT 
            CAST(ftl.device_time AS DATE) AS track_date,
            ftl.device_id,
            d.device_name,
            d.serial_number,
            s.site_code,
            ftl.latitude,
            ftl.longitude,
            ftl.speed_kmh,
            LAG(ftl.latitude) OVER (PARTITION BY ftl.device_id ORDER BY ftl.device_time) AS prev_lat,
            LAG(ftl.longitude) OVER (PARTITION BY ftl.device_id ORDER BY ftl.device_time) AS prev_lng,
            LAG(ftl.device_time) OVER (PARTITION BY ftl.device_id ORDER BY ftl.device_time) AS prev_time,
            DATEDIFF(SECOND, LAG(ftl.device_time) OVER (PARTITION BY ftl.device_id ORDER BY ftl.device_time), ftl.device_time) AS time_diff_sec
        FROM tbl_t_fleet_tracking_logs ftl
        INNER JOIN tbl_m_devices d ON d.id = ftl.device_id
        LEFT JOIN tbl_m_sites s ON s.id = d.site_id
        WHERE ftl.device_time BETWEEN @FromDT AND @ToDT
            AND ftl.latitude IS NOT NULL
            AND ftl.longitude IS NOT NULL
            AND ftl.gps_status != 'Not Fixed'
            AND ftl.latitude != 0.0
            AND ftl.longitude != 0.0
            AND (@SiteID IS NULL OR s.id = @SiteID)
    ),
    DistanceCalc AS (
        SELECT 
            track_date,
            device_id,
            device_name,
            serial_number,
            site_code,
            speed_kmh,
            time_diff_sec,
            CASE 
                WHEN prev_lat IS NOT NULL
                THEN (
                    111111 * DEGREES(
                        ACOS(
                            SIN(RADIANS(latitude)) * SIN(RADIANS(prev_lat))
                            + COS(RADIANS(latitude)) * COS(RADIANS(prev_lat))
                            * COS(RADIANS(longitude - prev_lng))
                        )
                    )
                ) / 1000.0
                ELSE 0 
            END AS distance_km,
            CASE 
                WHEN speed_kmh < 5 AND time_diff_sec > 300 THEN 1
                ELSE 0 
            END AS is_stop
        FROM TrackData
    )
    SELECT 
        track_date,
        site_code,
        device_id,
        device_name,
        serial_number,
        COUNT(*) AS total_points,
        ISNULL(ROUND(SUM(distance_km), 2), 0) AS total_distance_km,
        ISNULL(COUNT(CASE WHEN is_stop = 1 THEN 1 END), 0) AS total_stops,
        ISNULL(SUM(CASE WHEN speed_kmh < 5 THEN time_diff_sec ELSE 0 END), 0) AS idle_time_seconds,
        ISNULL(ROUND(AVG(speed_kmh), 1), 0) AS avg_speed_kmh,
        ISNULL(MAX(speed_kmh), 0) AS max_speed_kmh
    FROM DistanceCalc
    GROUP BY track_date, site_code, device_id, device_name, serial_number
    ORDER BY track_date DESC, site_code, device_name;
END;
GO

-- =============================================
-- Done: Tracking Stored Procedures
-- =============================================

GO

-- === 07_stored_procedure_ups.sql ===
IF OBJECT_ID('[dbo].[sp_GetLatestUpsStatus]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetLatestUpsStatus];
GO
CREATE PROCEDURE [dbo].[sp_GetLatestUpsStatus]
	@DeviceID UNIQUEIDENTIFIER = NULL,
	@SiteID UNIQUEIDENTIFIER = NULL,
	@ZoneID UNIQUEIDENTIFIER = NULL
AS
BEGIN
	SET NOCOUNT ON;

	;WITH device_site AS (
		SELECT
			d.id,
			d.id AS device_id,
			d.device_name,
			d.ip_address,
			d.is_active,
			d.zone_id,
			d.ups_phase_type,
			z.zone_name,
			COALESCE(z.site_id, d.site_id) AS site_id
		FROM tbl_m_devices d
		LEFT JOIN tbl_m_zones z ON z.id = d.zone_id
	)

	SELECT TOP 1
		u.[id],
		u.[device_id],
		ds.[device_name],
		ds.[ip_address],
		ds.[is_active],
		ds.[zone_id],
		ds.[zone_name],
		s.[id] AS [site_id],
		s.[site_name],
		ds.[ups_phase_type],
		u.[device_time],
		u.[voltage_in_a],
		u.[voltage_in_b],
		u.[voltage_in_c],
		u.[current_in_a],
		u.[current_in_b],
		u.[current_in_c],
		u.[input_frequency],
		u.[voltage_out_a],
		u.[voltage_out_b],
		u.[voltage_out_c],
		u.[current_out_a],
		u.[current_out_b],
		u.[current_out_c],
		u.[output_frequency],
		u.[battery_percent],
		u.[battery_voltage],
		u.[battery_runtime_min],
		u.[load_percent],
		u.[total_watts],
		u.[ambient_temp],
		u.[room_temp],
		u.[ups_status],
		u.[grounding_resistance]
	FROM [dbo].[tbl_t_ups_logs] u
	INNER JOIN device_site ds ON ds.device_id = u.device_id
	LEFT JOIN [dbo].[tbl_m_sites] s ON s.[id] = ds.site_id
	WHERE (@DeviceID IS NULL OR u.[device_id] = @DeviceID)
	  AND (@SiteID IS NULL OR s.[id] = @SiteID)
	  AND (@ZoneID IS NULL OR ds.[zone_id] = @ZoneID)
	ORDER BY u.[device_time] DESC;
END

GO

IF OBJECT_ID('[dbo].[sp_GetUpsSites]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetUpsSites];
GO
CREATE PROCEDURE [dbo].[sp_GetUpsSites]
    @OnlyWithUps BIT = 0
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @UpsTypeID UNIQUEIDENTIFIER = (
		SELECT TOP 1 id FROM tbl_r_asset_types WHERE UPPER(LTRIM(RTRIM(type_name))) = 'UPS'
	);

	;WITH device_site AS (
		SELECT
			d.id,
			d.asset_type_id,
			d.is_active,
			d.ip_address,
			COALESCE(z.site_id, d.site_id) AS site_id
		FROM tbl_m_devices d
		LEFT JOIN tbl_m_zones z ON z.id = d.zone_id
	)

	SELECT
		s.id,
		s.site_code,
		s.site_name,
		SUM(CASE WHEN ds.asset_type_id = @UpsTypeID AND ds.is_active = 1 THEN 1 ELSE 0 END) AS ups_count
	FROM tbl_m_sites s
	LEFT JOIN device_site ds ON ds.site_id = s.id
	GROUP BY s.id, s.site_code, s.site_name
	HAVING (@OnlyWithUps = 0)
		OR SUM(CASE WHEN ds.asset_type_id = @UpsTypeID AND ds.is_active = 1 THEN 1 ELSE 0 END) > 0
	ORDER BY s.site_code;
END

GO

IF OBJECT_ID('[dbo].[sp_GetUpsDevicesBySite]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetUpsDevicesBySite];
GO
CREATE PROCEDURE [dbo].[sp_GetUpsDevicesBySite]
	@SiteID UNIQUEIDENTIFIER
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @UpsTypeID UNIQUEIDENTIFIER = (
		SELECT TOP 1 id FROM tbl_r_asset_types WHERE UPPER(LTRIM(RTRIM(type_name))) = 'UPS'
	);

	;WITH device_site AS (
		SELECT
			d.id,
			d.device_name,
			d.asset_type_id,
			d.ip_address,
			d.is_active,
			d.zone_id,
			z.zone_name,
			COALESCE(z.site_id, d.site_id) AS site_id
		FROM tbl_m_devices d
		LEFT JOIN tbl_m_zones z ON z.id = d.zone_id
	)

	SELECT
		ds.id,
		ds.device_name,
		ds.ip_address,
		ds.is_active,
		ds.zone_id,
		ds.zone_name,
		s.id AS site_id,
		s.site_code,
		s.site_name
	FROM device_site ds
	JOIN tbl_m_sites s ON s.id = ds.site_id
	WHERE ds.asset_type_id = @UpsTypeID
	  AND ds.is_active = 1
	  AND ds.ip_address IS NOT NULL
	  AND LTRIM(RTRIM(ds.ip_address)) <> ''
	  AND s.id = @SiteID
	ORDER BY ds.zone_name, ds.device_name;
END

GO

IF OBJECT_ID('[dbo].[sp_GetUpsVoltageHistory]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetUpsVoltageHistory];
GO
CREATE PROCEDURE [dbo].[sp_GetUpsVoltageHistory]
	@DeviceID UNIQUEIDENTIFIER,
	@Hours INT = 24,
	@Limit INT = 96
AS
BEGIN
	SET NOCOUNT ON;

	SELECT TOP (@Limit)
		u.device_time,
		(ISNULL(u.voltage_in_a, 0) + ISNULL(u.voltage_in_b, 0) + ISNULL(u.voltage_in_c, 0)) / 
            CASE WHEN d.ups_phase_type = '1/1' THEN 1.0 ELSE 3.0 END AS voltage_in_avg,
		(ISNULL(u.voltage_out_a, 0) + ISNULL(u.voltage_out_b, 0) + ISNULL(u.voltage_out_c, 0)) / 
            CASE WHEN d.ups_phase_type = '1/1' OR d.ups_phase_type = '3/1' THEN 1.0 ELSE 3.0 END AS voltage_out_avg
	FROM tbl_t_ups_logs u
	INNER JOIN tbl_m_devices d ON d.id = u.device_id
	WHERE u.device_id = @DeviceID
	  AND u.device_time >= DATEADD(HOUR, -@Hours, GETDATE())
	ORDER BY u.device_time DESC;
END

GO

GO

-- === 07_tracking_hourly_statistics.sql ===
-- =============================================
-- TRACKING HOURLY STATISTICS PROCEDURE
-- Get hourly aggregated speed data untuk real-time chart
-- Untuk MQTT + Real-time integration
-- =============================================

-- =============================================
-- sp_GetTrackingHourlyStatistics
-- Get hourly stats untuk speed chart (real data only)
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetTrackingHourlyStatistics]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetTrackingHourlyStatistics];
GO
CREATE PROCEDURE [dbo].[sp_GetTrackingHourlyStatistics]
    @DeviceID UNIQUEIDENTIFIER,
    @DateFrom DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDT DATETIME = CAST(@DateFrom AS DATETIME);
    DECLARE @ToDT DATETIME = CAST(@DateFrom AS DATETIME) + CAST('23:59:59' AS DATETIME);

    -- Aggregate speed data per hour
    ;WITH HourlyAggregates AS (
        SELECT
            DATEPART(HOUR, device_time) AS hour,
            COUNT(*) AS point_count,
            AVG(speed_kmh) AS avg_speed,
            MAX(speed_kmh) AS max_speed,
            MIN(CASE WHEN speed_kmh > 0 THEN speed_kmh ELSE NULL END) AS min_speed,
            SUM(CASE WHEN speed_kmh < 5 THEN 1 ELSE 0 END) AS idle_point_count
        FROM tbl_t_fleet_tracking_logs
        WHERE device_id = @DeviceID
            AND device_time BETWEEN @FromDT AND @ToDT
            AND speed_kmh IS NOT NULL
        GROUP BY DATEPART(HOUR, device_time)
    )
    
    -- Generate all 24 hours, with NULL untuk hours tanpa data
    SELECT
        CAST(AllHours.hour AS INT) AS hour,
        ISNULL(HourlyAggregates.point_count, 0) AS point_count,
        ISNULL(ROUND(HourlyAggregates.avg_speed, 1), NULL) AS avg_speed_kmh,
        ISNULL(ROUND(HourlyAggregates.max_speed, 1), NULL) AS max_speed_kmh,
        ISNULL(ROUND(HourlyAggregates.min_speed, 1), NULL) AS min_speed_kmh,
        ISNULL(HourlyAggregates.idle_point_count, 0) AS idle_points,
        CASE 
            WHEN ISNULL(HourlyAggregates.point_count, 0) > 0 THEN 1
            ELSE 0
        END AS has_data
    FROM (
        SELECT 0 AS hour UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL
        SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL
        SELECT 8 UNION ALL SELECT 9 UNION ALL SELECT 10 UNION ALL SELECT 11 UNION ALL
        SELECT 12 UNION ALL SELECT 13 UNION ALL SELECT 14 UNION ALL SELECT 15 UNION ALL
        SELECT 16 UNION ALL SELECT 17 UNION ALL SELECT 18 UNION ALL SELECT 19 UNION ALL
        SELECT 20 UNION ALL SELECT 21 UNION ALL SELECT 22 UNION ALL SELECT 23
    ) AS AllHours
    LEFT JOIN HourlyAggregates ON AllHours.hour = HourlyAggregates.hour
    ORDER BY AllHours.hour ASC;
END;
GO

-- =============================================
-- sp_GetTrackingRealtimeStats
-- Get total distance + speed stats untuk device hari ini (real-time)
-- Useful untuk live dashboard updates
-- =============================================
IF OBJECT_ID('[dbo].[sp_GetTrackingRealtimeStats]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetTrackingRealtimeStats];
GO
CREATE PROCEDURE [dbo].[sp_GetTrackingRealtimeStats]
    @DeviceID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TodayStart DATETIME = CAST(CAST(GETUTCDATE() AS DATE) AS DATETIME);
    DECLARE @TodayEnd DATETIME = CAST(GETUTCDATE() AS DATETIME);

    ;WITH TrackingToday AS (
        SELECT 
            latitude,
            longitude,
            speed_kmh,
            device_time,
            LAG(latitude) OVER (ORDER BY device_time) AS prev_lat,
            LAG(longitude) OVER (ORDER BY device_time) AS prev_lng
        FROM tbl_t_fleet_tracking_logs
        WHERE device_id = @DeviceID
            AND device_time BETWEEN @TodayStart AND @TodayEnd
            AND latitude IS NOT NULL
            AND longitude IS NOT NULL
    ),
    DistanceCalc AS (
        SELECT 
            speed_kmh,
            device_time,
            CASE 
                WHEN prev_lat IS NOT NULL
                THEN (
                    111111 * DEGREES(
                        ACOS(
                            CASE 
                                WHEN (SIN(RADIANS(latitude)) * SIN(RADIANS(prev_lat))
                                    + COS(RADIANS(latitude)) * COS(RADIANS(prev_lat))
                                    * COS(RADIANS(longitude - prev_lng))) > 1
                                THEN 1
                                WHEN (SIN(RADIANS(latitude)) * SIN(RADIANS(prev_lat))
                                    + COS(RADIANS(latitude)) * COS(RADIANS(prev_lat))
                                    * COS(RADIANS(longitude - prev_lng))) < -1
                                THEN -1
                                ELSE (SIN(RADIANS(latitude)) * SIN(RADIANS(prev_lat))
                                    + COS(RADIANS(latitude)) * COS(RADIANS(prev_lat))
                                    * COS(RADIANS(longitude - prev_lng)))
                            END
                        )
                    ) / 1000.0
                )
                ELSE 0 
            END AS distance_km
        FROM TrackingToday
    )
    SELECT 
        @DeviceID AS device_id,
        CAST(GETUTCDATE() AS DATE) AS report_date,
                COUNT(*) AS total_points,
        ISNULL(ROUND(SUM(distance_km), 2), 0) AS total_distance_km,
        ISNULL(ROUND(AVG(speed_kmh), 1), 0) AS avg_speed_kmh,
        ISNULL(MAX(speed_kmh), 0) AS max_speed_kmh,
                (SELECT TOP 1 device_time FROM tbl_t_fleet_tracking_logs 
                 WHERE device_id = @DeviceID
                     AND latitude IS NOT NULL
                     AND longitude IS NOT NULL
                 ORDER BY device_time DESC) AS last_update,
        DATEDIFF(MINUTE, 
            (SELECT TOP 1 device_time FROM tbl_t_fleet_tracking_logs 
                         WHERE device_id = @DeviceID AND device_time >= @TodayStart
                             AND latitude IS NOT NULL AND longitude IS NOT NULL
             ORDER BY device_time ASC),
            (SELECT TOP 1 device_time FROM tbl_t_fleet_tracking_logs 
                         WHERE device_id = @DeviceID AND device_time >= @TodayStart
                             AND latitude IS NOT NULL AND longitude IS NOT NULL
             ORDER BY device_time DESC)
        ) AS active_duration_minutes
    FROM DistanceCalc;
END;
GO

-- =============================================
-- Done: Hourly Statistics Procedures
-- =============================================

GO


IF OBJECT_ID('[dbo].[sp_IsUserAdministrator]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_IsUserAdministrator];
GO
CREATE PROCEDURE [dbo].[sp_IsUserAdministrator]
    @UserID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE
        WHEN EXISTS (
            SELECT 1
            FROM dbo.tbl_m_users u
            LEFT JOIN dbo.tbl_r_roles r ON r.id = u.role_id
            WHERE u.id = @UserID
              AND (UPPER(LTRIM(RTRIM(ISNULL(r.role_name, '')))) = 'ADMINISTRATOR')
        ) THEN CAST(1 AS BIT)
        ELSE CAST(0 AS BIT)
    END AS is_admin;
END;
GO

IF OBJECT_ID('[dbo].[sp_GetUserAssignedSites]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_GetUserAssignedSites];
GO
CREATE PROCEDURE [dbo].[sp_GetUserAssignedSites]
    @UserID UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @UserID IS NULL OR EXISTS (
        SELECT 1 FROM dbo.tbl_m_users u
        LEFT JOIN dbo.tbl_r_roles r ON r.id = u.role_id
        WHERE u.id = @UserID AND (r.role_name IN ('Administrator', 'NOC', 'Tracking'))
    ) OR NOT EXISTS (
        SELECT 1 FROM dbo.tbl_r_user_assignments ua WHERE ua.user_id = @UserID
    )
    BEGIN
        SELECT s.id AS site_id, s.site_code, s.site_name, s.latitude, s.longitude
        FROM dbo.tbl_m_sites s
        ORDER BY s.site_code;
        RETURN;
    END

    SELECT DISTINCT s.id AS site_id, s.site_code, s.site_name, s.latitude, s.longitude
    FROM dbo.tbl_r_user_assignments ua
    JOIN dbo.tbl_m_sites s ON s.id = ua.site_id
    WHERE ua.user_id = @UserID
    ORDER BY s.site_code;
END;
GO

IF OBJECT_ID('[dbo].[sp_ReportTrendPerDevice]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[sp_ReportTrendPerDevice];
GO
CREATE PROCEDURE [dbo].[sp_ReportTrendPerDevice]
    @SiteID UNIQUEIDENTIFIER = NULL,
    @DateFrom DATE = NULL,
    @DateTo DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @FromDT DATETIME2 = CAST(ISNULL(@DateFrom, DATEADD(DAY, -7, CAST(GETDATE() AS DATE))) AS DATETIME2);
    DECLARE @ToDT   DATETIME2 = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(ISNULL(@DateTo, CAST(GETDATE() AS DATE)) AS DATETIME2)));

    SELECT 'voltage' AS metric, d.device_name, CONVERT(VARCHAR(16), g.device_time, 120) AS time_label, CAST(g.voltage AS FLOAT) AS value, CAST(ISNULL(d.asset_height, 0) AS FLOAT) AS asset_height
    FROM dbo.tbl_t_genset_logs g JOIN dbo.tbl_m_devices d ON d.id = g.device_id
    WHERE (@SiteID IS NULL OR d.site_id = @SiteID) AND g.device_time BETWEEN @FromDT AND @ToDT AND g.voltage IS NOT NULL
    UNION ALL
    SELECT 'fuel_level' AS metric, d.device_name, CONVERT(VARCHAR(16), g.device_time, 120) AS time_label, CAST(g.fuel_level AS FLOAT) AS value, CAST(ISNULL(d.asset_height, 0) AS FLOAT) AS asset_height
    FROM dbo.tbl_t_genset_logs g JOIN dbo.tbl_m_devices d ON d.id = g.device_id
    WHERE (@SiteID IS NULL OR d.site_id = @SiteID) AND g.device_time BETWEEN @FromDT AND @ToDT AND g.fuel_level IS NOT NULL
    UNION ALL
    SELECT 'wind_speed' AS metric, d.device_name, CONVERT(VARCHAR(16), t.device_time, 120) AS time_label, CAST(t.wind_speed AS FLOAT) AS value, CAST(ISNULL(d.asset_height, 42) AS FLOAT) AS asset_height
    FROM dbo.tbl_t_tower_logs t JOIN dbo.tbl_m_devices d ON d.id = t.device_id
    WHERE (@SiteID IS NULL OR d.site_id = @SiteID) AND t.device_time BETWEEN @FromDT AND @ToDT AND t.wind_speed IS NOT NULL
    UNION ALL
    SELECT 'rainfall' AS metric, d.device_name, CONVERT(VARCHAR(16), t.device_time, 120) AS time_label, CAST(t.rainfall AS FLOAT) AS value, CAST(ISNULL(d.asset_height, 42) AS FLOAT) AS asset_height
    FROM dbo.tbl_t_tower_logs t JOIN dbo.tbl_m_devices d ON d.id = t.device_id
    WHERE (@SiteID IS NULL OR d.site_id = @SiteID) AND t.device_time BETWEEN @FromDT AND @ToDT AND t.rainfall IS NOT NULL
    UNION ALL
    SELECT 'luminosity' AS metric, d.device_name, CONVERT(VARCHAR(16), t.device_time, 120) AS time_label, CAST(COALESCE(t.luminosity, t.illuminance) AS FLOAT) AS value, CAST(ISNULL(d.asset_height, 42) AS FLOAT) AS asset_height
    FROM dbo.tbl_t_tower_logs t JOIN dbo.tbl_m_devices d ON d.id = t.device_id
    WHERE (@SiteID IS NULL OR d.site_id = @SiteID) AND t.device_time BETWEEN @FromDT AND @ToDT AND COALESCE(t.luminosity, t.illuminance) IS NOT NULL
    UNION ALL
    SELECT 'dust_level' AS metric, d.device_name, CONVERT(VARCHAR(16), t.device_time, 120) AS time_label, CAST(COALESCE(t.dust_level, t.pm2_5, t.pm10) AS FLOAT) AS value, CAST(ISNULL(d.asset_height, 42) AS FLOAT) AS asset_height
    FROM dbo.tbl_t_tower_logs t JOIN dbo.tbl_m_devices d ON d.id = t.device_id
    WHERE (@SiteID IS NULL OR d.site_id = @SiteID) AND t.device_time BETWEEN @FromDT AND @ToDT AND COALESCE(t.dust_level, t.pm2_5, t.pm10) IS NOT NULL
    UNION ALL
    SELECT 'temperature' AS metric, d.device_name, CONVERT(VARCHAR(16), t.device_time, 120) AS time_label, CAST(t.temperature AS FLOAT) AS value, CAST(ISNULL(d.asset_height, 42) AS FLOAT) AS asset_height
    FROM dbo.tbl_t_tower_logs t JOIN dbo.tbl_m_devices d ON d.id = t.device_id
    WHERE (@SiteID IS NULL OR d.site_id = @SiteID) AND t.device_time BETWEEN @FromDT AND @ToDT AND t.temperature IS NOT NULL
    UNION ALL
    SELECT 'tilt' AS metric, d.device_name, CONVERT(VARCHAR(16), t.device_time, 120) AS time_label, (ABS(CAST(t.tilt_x AS FLOAT)) + ABS(CAST(t.tilt_y AS FLOAT))) / 2.0 AS value, CAST(ISNULL(d.asset_height, 42) AS FLOAT) AS asset_height
    FROM dbo.tbl_t_tower_logs t JOIN dbo.tbl_m_devices d ON d.id = t.device_id
    WHERE (@SiteID IS NULL OR d.site_id = @SiteID) AND t.device_time BETWEEN @FromDT AND @ToDT AND (t.tilt_x IS NOT NULL OR t.tilt_y IS NOT NULL)
    UNION ALL
    SELECT 'ground_resistance' AS metric, d.device_name, CONVERT(VARCHAR(16), t.device_time, 120) AS time_label, CAST(t.ground_resistance AS FLOAT) AS value, CAST(ISNULL(d.asset_height, 42) AS FLOAT) AS asset_height
    FROM dbo.tbl_t_tower_logs t JOIN dbo.tbl_m_devices d ON d.id = t.device_id
    WHERE (@SiteID IS NULL OR d.site_id = @SiteID) AND t.device_time BETWEEN @FromDT AND @ToDT AND t.ground_resistance IS NOT NULL
    UNION ALL
    SELECT 'lightning_current' AS metric, d.device_name, CONVERT(VARCHAR(16), t.device_time, 120) AS time_label, CAST(t.lightning_current AS FLOAT) AS value, CAST(ISNULL(d.asset_height, 42) AS FLOAT) AS asset_height
    FROM dbo.tbl_t_tower_logs t JOIN dbo.tbl_m_devices d ON d.id = t.device_id
    WHERE (@SiteID IS NULL OR d.site_id = @SiteID) AND t.device_time BETWEEN @FromDT AND @ToDT AND t.lightning_current IS NOT NULL;
END;
GO
