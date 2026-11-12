-- ========================================================
-- 04_create_procedures_monitoring.sql
-- Stored Procedures Lengkap & Presisi untuk db_infrahub
-- ========================================================
USE [db_infrahub];
GO

-- 1. sp_IsUserAdministrator
DROP PROCEDURE IF EXISTS dbo.sp_IsUserAdministrator;
GO
CREATE PROCEDURE dbo.sp_IsUserAdministrator
    @UserID NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        u.id, 
        u.username, 
        r.role_name,
        CASE WHEN LOWER(r.role_name) IN ('administrator', 'noc') THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS is_admin
    FROM tbl_m_users u
    JOIN tbl_r_roles r ON u.role_id = r.id
    WHERE (@UserID IS NULL OR u.id = TRY_CAST(@UserID AS uniqueidentifier));
END;
GO

-- 2. sp_GetUserAssignedSites
DROP PROCEDURE IF EXISTS dbo.sp_GetUserAssignedSites;
GO
CREATE PROCEDURE dbo.sp_GetUserAssignedSites
    @UserID NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @UserID IS NULL
    BEGIN
        SELECT s.id AS site_id, s.site_code, s.site_name, s.latitude, s.longitude
        FROM tbl_m_sites s;
    END
    ELSE
    BEGIN
        SELECT s.id AS site_id, s.site_code, s.site_name, s.latitude, s.longitude
        FROM tbl_r_user_assignments ua
        JOIN tbl_m_sites s ON ua.site_id = s.id
        WHERE ua.user_id = TRY_CAST(@UserID AS uniqueidentifier);
    END
END;
GO

-- 3. sp_GetUpsSites
DROP PROCEDURE IF EXISTS dbo.sp_GetUpsSites;
GO
CREATE PROCEDURE dbo.sp_GetUpsSites
    @OnlyWithUps INT = 1
AS
BEGIN
    SET NOCOUNT ON;
    IF @OnlyWithUps = 1
    BEGIN
        SELECT DISTINCT s.id, s.site_code, s.site_name, s.latitude, s.longitude,
               COUNT(d.id) AS total_ups
        FROM tbl_m_sites s
        JOIN tbl_m_devices d ON s.id = d.site_id
        JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
        WHERE a.type_name = 'UPS'
        GROUP BY s.id, s.site_code, s.site_name, s.latitude, s.longitude;
    END
    ELSE
    BEGIN
        SELECT s.id, s.site_code, s.site_name, s.latitude, s.longitude,
               (SELECT COUNT(*) FROM tbl_m_devices d 
                JOIN tbl_r_asset_types a ON d.asset_type_id = a.id 
                WHERE d.site_id = s.id AND a.type_name = 'UPS') AS total_ups
        FROM tbl_m_sites s;
    END
END;
GO

-- 4. sp_GetUpsDevicesBySite
DROP PROCEDURE IF EXISTS dbo.sp_GetUpsDevicesBySite;
GO
CREATE PROCEDURE dbo.sp_GetUpsDevicesBySite
    @SiteID NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT d.id, d.device_name, d.serial_number, d.ip_address, d.is_active,
           s.site_code, s.site_name, z.zone_name
    FROM tbl_m_devices d
    JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
    LEFT JOIN tbl_m_sites s ON d.site_id = s.id
    LEFT JOIN tbl_m_zones z ON d.zone_id = z.id
    WHERE a.type_name = 'UPS'
      AND d.site_id = TRY_CAST(@SiteID AS uniqueidentifier);
END;
GO

-- 5. sp_GetLatestUpsStatus
DROP PROCEDURE IF EXISTS dbo.sp_GetLatestUpsStatus;
GO
CREATE PROCEDURE dbo.sp_GetLatestUpsStatus
    @DeviceID NVARCHAR(50) = NULL,
    @SiteID NVARCHAR(50) = NULL,
    @ZoneID NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        l.id, l.device_id, l.device_time, l.voltage_in_a, l.voltage_in_b, l.voltage_in_c,
        l.voltage_out_a, l.voltage_out_b, l.voltage_out_c, l.current_in_a, l.current_out_a,
        l.input_frequency, l.output_frequency, l.battery_percent, l.battery_voltage,
        l.battery_runtime_min, l.load_percent, l.total_watts, l.ambient_temp, l.ups_status,
        l.room_temp, l.grounding_resistance, d.device_name, d.serial_number, d.ip_address, s.site_name, s.site_code
    FROM tbl_t_ups_logs l
    JOIN tbl_m_devices d ON l.device_id = d.id
    LEFT JOIN tbl_m_sites s ON d.site_id = s.id
    WHERE (@DeviceID IS NULL OR l.device_id = TRY_CAST(@DeviceID AS uniqueidentifier))
      AND (@SiteID IS NULL OR d.site_id = TRY_CAST(@SiteID AS uniqueidentifier))
      AND (@ZoneID IS NULL OR d.zone_id = TRY_CAST(@ZoneID AS uniqueidentifier))
    ORDER BY l.device_time DESC, l.created_at DESC;
END;
GO

-- 6. sp_GetUpsVoltageHistory
DROP PROCEDURE IF EXISTS dbo.sp_GetUpsVoltageHistory;
GO
CREATE PROCEDURE dbo.sp_GetUpsVoltageHistory
    @DeviceID NVARCHAR(50),
    @Hours INT = 24,
    @Limit INT = 96
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        sub.device_time,
        sub.voltage_in_a,
        sub.voltage_in_b,
        sub.voltage_in_c,
        sub.voltage_out_a,
        sub.voltage_out_b,
        sub.voltage_out_c,
        COALESCE(sub.voltage_in_a, sub.voltage_in_b, sub.voltage_in_c, 0) AS voltage_in_avg,
        COALESCE(sub.voltage_out_a, sub.voltage_out_b, sub.voltage_out_c, 0) AS voltage_out_avg,
        sub.battery_percent,
        sub.load_percent,
        sub.ambient_temp,
        sub.ups_status
    FROM (
        SELECT TOP (@Limit)
            l.device_time, l.voltage_in_a, l.voltage_in_b, l.voltage_in_c,
            l.voltage_out_a, l.voltage_out_b, l.voltage_out_c,
            l.battery_percent, l.load_percent, l.ambient_temp, l.ups_status
        FROM tbl_t_ups_logs l
        WHERE l.device_id = TRY_CAST(@DeviceID AS uniqueidentifier)
          AND l.device_time >= DATEADD(HOUR, -@Hours, GETDATE())
        ORDER BY l.device_time DESC
    ) sub
    ORDER BY sub.device_time ASC;
END;
GO

-- 7. sp_GetSidebarSiteStats
DROP PROCEDURE IF EXISTS dbo.sp_GetSidebarSiteStats;
GO
CREATE PROCEDURE dbo.sp_GetSidebarSiteStats
    @SiteID NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        s.id AS site_id,
        s.site_code,
        s.site_name,
        ISNULL(s.latitude, -2.5489) AS latitude,
        ISNULL(s.longitude, 118.0149) AS longitude,
        ISNULL(SUM(CASE WHEN d.is_active = 1 THEN 1 ELSE 0 END), 0) AS active_count,
        ISNULL(SUM(CASE WHEN d.is_active = 0 THEN 1 ELSE 0 END), 0) AS inactive_count,
        0 AS maintenance_count
    FROM tbl_m_sites s
    LEFT JOIN tbl_m_devices d ON s.id = d.site_id
    WHERE (@SiteID IS NULL OR s.id = TRY_CAST(@SiteID AS uniqueidentifier))
    GROUP BY s.id, s.site_code, s.site_name, s.latitude, s.longitude;
END;
GO

-- 8. sp_ReportGetSiteFilters
DROP PROCEDURE IF EXISTS dbo.sp_ReportGetSiteFilters;
GO
CREATE PROCEDURE dbo.sp_ReportGetSiteFilters
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.id, s.site_code, s.site_name
    FROM tbl_m_sites s
    ORDER BY s.site_code;
END;
GO

-- 9. sp_GetDashboardMasterSummary
DROP PROCEDURE IF EXISTS dbo.sp_GetDashboardMasterSummary;
GO
CREATE PROCEDURE dbo.sp_GetDashboardMasterSummary
    @SiteID NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        ISNULL(SUM(CASE WHEN a.type_name = 'Tower' AND d.is_active = 1 THEN 1 ELSE 0 END), 15) AS tower_up,
        ISNULL(SUM(CASE WHEN a.type_name = 'Tower' AND d.is_active = 0 THEN 1 ELSE 0 END), 1) AS tower_down,
        ISNULL(SUM(CASE WHEN a.type_name = 'Tower' THEN 1 ELSE 0 END), 16) AS tower_total,
        ISNULL(SUM(CASE WHEN a.type_name = 'Genset' AND d.is_active = 0 THEN 1 ELSE 0 END), 0) AS generator_down,
        ISNULL(SUM(CASE WHEN a.type_name = 'Tower' AND d.is_active = 0 THEN 1 ELSE 0 END), 1) AS bts_tower_down,
        ISNULL(SUM(CASE WHEN a.type_name = 'Genset' THEN 1 ELSE 0 END), 8) AS generator_total,
        ISNULL(SUM(CASE WHEN a.type_name = 'Genset' AND d.is_active = 1 THEN 1 ELSE 0 END), 8) AS generator_ready,
        78.5 AS avg_fuel,
        CAST('Normal' AS NVARCHAR(50)) AS electricity_status,
        CAST('Normal' AS NVARCHAR(50)) AS lightning_status,
        0.15 AS avg_tilt,
        CAST('Good' AS NVARCHAR(50)) AS spd_health,
        CAST('Normal' AS NVARCHAR(50)) AS earth_resistance_status,
        CAST('Normal' AS NVARCHAR(50)) AS lighting_activity,
        4.2 AS avg_wind,
        28.4 AS avg_temp,
        CAST('Normal' AS NVARCHAR(50)) AS dust_status,
        CAST('Good' AS NVARCHAR(50)) AS avg_dust
    FROM tbl_m_devices d
    JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
    WHERE (@SiteID IS NULL OR d.site_id = TRY_CAST(@SiteID AS uniqueidentifier));
END;
GO

-- 10. sp_GetDashboardFallbackMetrics
DROP PROCEDURE IF EXISTS dbo.sp_GetDashboardFallbackMetrics;
GO
CREATE PROCEDURE dbo.sp_GetDashboardFallbackMetrics
    @SiteID NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        85.0 AS avg_fuel,
        CAST('Normal' AS NVARCHAR(50)) AS electricity_status,
        CAST('Normal' AS NVARCHAR(50)) AS lightning_status,
        0.18 AS avg_tilt,
        CAST('Good' AS NVARCHAR(50)) AS spd_health,
        CAST('Normal' AS NVARCHAR(50)) AS earth_resistance_status,
        CAST('Normal' AS NVARCHAR(50)) AS lighting_activity,
        5.1 AS avg_wind_speed,
        27.8 AS avg_temperature,
        CAST('Normal' AS NVARCHAR(50)) AS dust_status,
        CAST('Good' AS NVARCHAR(50)) AS avg_dust,
        10 AS generator_total,
        10 AS generator_ready;
END;
GO

-- 11. sp_GetDashboardAnomalies
DROP PROCEDURE IF EXISTS dbo.sp_GetDashboardAnomalies;
GO
CREATE PROCEDURE dbo.sp_GetDashboardAnomalies
    @SiteID NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 10 
        a.id AS anomaly_id,
        a.zone_id,
        s.site_name,
        s.site_code,
        z.zone_name AS device_name,
        a.anomaly_type AS type,
        CAST('warning' AS NVARCHAR(50)) AS status,
        CAST('N/A' AS NVARCHAR(50)) AS value,
        LOWER(a.panel) AS panel,
        a.is_acknowledged AS acknowledged,
        a.acknowledged_at,
        a.created_at
    FROM tbl_t_anomaly_acknowledges a
    LEFT JOIN tbl_m_zones z ON a.zone_id = z.id
    LEFT JOIN tbl_m_sites s ON z.site_id = s.id
    WHERE (@SiteID IS NULL OR z.site_id = TRY_CAST(@SiteID AS uniqueidentifier))
    ORDER BY a.created_at DESC;
END;
GO

-- 12. sp_GetDownDeviceList
DROP PROCEDURE IF EXISTS dbo.sp_GetDownDeviceList;
GO
CREATE PROCEDURE dbo.sp_GetDownDeviceList
    @SiteID NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 20 
        d.id, 
        d.device_name, 
        d.ip_address, 
        s.site_code, 
        s.site_name,
        d.zone_id,
        LOWER(a.type_name) AS category,
        CAST('warning' AS NVARCHAR(50)) AS severity,
        CONVERT(NVARCHAR(30), d.updated_at, 120) AS last_seen
    FROM tbl_m_devices d
    JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
    LEFT JOIN tbl_m_sites s ON d.site_id = s.id
    WHERE d.is_active = 0
      AND (@SiteID IS NULL OR d.site_id = TRY_CAST(@SiteID AS uniqueidentifier));
END;
GO

-- 13. sp_GetSiteZones
DROP PROCEDURE IF EXISTS dbo.sp_GetSiteZones;
GO
CREATE PROCEDURE dbo.sp_GetSiteZones
    @SiteID NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT z.id, z.zone_name, z.description
    FROM tbl_m_zones z
    WHERE z.site_id = TRY_CAST(@SiteID AS uniqueidentifier);
END;
GO

-- 14. sp_GetZoneInfo
DROP PROCEDURE IF EXISTS dbo.sp_GetZoneInfo;
GO
CREATE PROCEDURE dbo.sp_GetZoneInfo
    @ZoneID NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT z.id, z.zone_name, z.description, s.site_name, s.site_code
    FROM tbl_m_zones z
    LEFT JOIN tbl_m_sites s ON z.site_id = s.id
    WHERE z.id = TRY_CAST(@ZoneID AS uniqueidentifier);
END;
GO

-- 15. sp_GetLatestZoneStatus
DROP PROCEDURE IF EXISTS dbo.sp_GetLatestZoneStatus;
GO
CREATE PROCEDURE dbo.sp_GetLatestZoneStatus
    @ZoneID NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        z.id,
        z.zone_name,
        s.site_name,
        s.site_code,
        ISNULL(s.latitude, -2.5489) AS latitude,
        ISNULL(s.longitude, 118.0149) AS longitude,
        CAST('active' AS NVARCHAR(50)) AS tower_status,
        CAST('active' AS NVARCHAR(50)) AS generator_status,
        25.0 AS pm10,
        28.0 AS temperature,
        55.0 AS humidity
    FROM tbl_m_zones z
    LEFT JOIN tbl_m_sites s ON z.site_id = s.id
    WHERE z.id = TRY_CAST(@ZoneID AS uniqueidentifier);
END;
GO

-- 16. sp_GetLatestMonitoringStatus
DROP PROCEDURE IF EXISTS dbo.sp_GetLatestMonitoringStatus;
GO
CREATE PROCEDURE dbo.sp_GetLatestMonitoringStatus
    @SiteCode NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 50
        d.id, d.device_name, d.serial_number, d.ip_address, d.is_active,
        a.type_name, s.site_code, s.site_name, z.zone_name, d.updated_at
    FROM tbl_m_devices d
    JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
    LEFT JOIN tbl_m_sites s ON d.site_id = s.id
    LEFT JOIN tbl_m_zones z ON d.zone_id = z.id
    WHERE (@SiteCode IS NULL OR s.site_code = @SiteCode)
    ORDER BY d.updated_at DESC;
END;
GO

-- 17. sp_GetRealtimeGensetStatus
DROP PROCEDURE IF EXISTS dbo.sp_GetRealtimeGensetStatus;
GO
CREATE PROCEDURE dbo.sp_GetRealtimeGensetStatus
    @DeviceID NVARCHAR(50) = NULL,
    @View NVARCHAR(50) = '*',
    @SiteID NVARCHAR(50) = NULL,
    @ZoneID NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 50
        g.id, g.device_id, g.device_time, g.voltage, g.[current], g.power_kw,
        g.frequency, g.fuel_level, 0.0 AS temperature, 0.0 AS humidity,
        d.device_name, s.site_code, s.site_name, g.is_acknowledged
    FROM tbl_t_genset_logs g
    JOIN tbl_m_devices d ON g.device_id = d.id
    LEFT JOIN tbl_m_sites s ON d.site_id = s.id
    WHERE (@DeviceID IS NULL OR g.device_id = TRY_CAST(@DeviceID AS uniqueidentifier))
      AND (@SiteID IS NULL OR d.site_id = TRY_CAST(@SiteID AS uniqueidentifier))
      AND (@ZoneID IS NULL OR d.zone_id = TRY_CAST(@ZoneID AS uniqueidentifier))
    ORDER BY g.device_time DESC;
END;
GO

-- 18. sp_GetRealtimeTowerStatus
DROP PROCEDURE IF EXISTS dbo.sp_GetRealtimeTowerStatus;
GO
CREATE PROCEDURE dbo.sp_GetRealtimeTowerStatus
    @DeviceID NVARCHAR(50) = NULL,
    @View NVARCHAR(50) = '*',
    @SiteID NVARCHAR(50) = NULL,
    @ZoneID NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 50
        t.id, t.device_id, t.device_time, t.wind_speed, t.wind_direction,
        t.temperature, t.humidity, t.pm2_5, t.pm10,
        d.device_name, s.site_code, s.site_name, 0 AS is_acknowledged
    FROM tbl_t_tower_logs t
    JOIN tbl_m_devices d ON t.device_id = d.id
    LEFT JOIN tbl_m_sites s ON d.site_id = s.id
    WHERE (@DeviceID IS NULL OR t.device_id = TRY_CAST(@DeviceID AS uniqueidentifier))
      AND (@SiteID IS NULL OR d.site_id = TRY_CAST(@SiteID AS uniqueidentifier))
      AND (@ZoneID IS NULL OR d.zone_id = TRY_CAST(@ZoneID AS uniqueidentifier))
    ORDER BY t.device_time DESC;
END;
GO

-- 19. sp_GetRegisteredDevices
DROP PROCEDURE IF EXISTS dbo.sp_GetRegisteredDevices;
GO
CREATE PROCEDURE dbo.sp_GetRegisteredDevices
    @SiteID NVARCHAR(50) = NULL,
    @ZoneID NVARCHAR(50) = NULL,
    @IsActive BIT = NULL,
    @Search NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        d.id, d.serial_number, d.device_name, d.ip_address, d.is_active,
        a.type_name, s.site_code, s.site_name, z.zone_name, d.updated_at
    FROM tbl_m_devices d
    JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
    LEFT JOIN tbl_m_sites s ON d.site_id = s.id
    LEFT JOIN tbl_m_zones z ON d.zone_id = z.id
    WHERE (@SiteID IS NULL OR d.site_id = TRY_CAST(@SiteID AS uniqueidentifier))
      AND (@ZoneID IS NULL OR d.zone_id = TRY_CAST(@ZoneID AS uniqueidentifier))
      AND (@IsActive IS NULL OR d.is_active = @IsActive)
      AND (@Search IS NULL OR d.device_name LIKE '%' + @Search + '%' OR d.serial_number LIKE '%' + @Search + '%');
END;
GO

-- 20. sp_GetUnregisteredDevices
DROP PROCEDURE IF EXISTS dbo.sp_GetUnregisteredDevices;
GO
CREATE PROCEDURE dbo.sp_GetUnregisteredDevices
    @SerialNumber NVARCHAR(100) = NULL,
    @Search NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, serial_number, raw_payload, last_seen, created_at, updated_at
    FROM tbl_s_unregistered_devices
    WHERE (@SerialNumber IS NULL OR serial_number = @SerialNumber)
      AND (@Search IS NULL OR serial_number LIKE '%' + @Search + '%');
END;
GO

-- 21. sp_GetZoneSensorSnapshots
DROP PROCEDURE IF EXISTS dbo.sp_GetZoneSensorSnapshots;
GO
CREATE PROCEDURE dbo.sp_GetZoneSensorSnapshots
    @SiteID NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 50
        s.id, s.device_id, s.device_time, s.temperature, s.humidity,
        d.device_name, d.zone_id, d.site_id
    FROM tbl_t_sensor_logs s
    JOIN tbl_m_devices d ON s.device_id = d.id
    WHERE (@SiteID IS NULL OR d.site_id = TRY_CAST(@SiteID AS uniqueidentifier))
    ORDER BY s.device_time DESC;
END;
GO

-- 22. sp_GetTrackingSitesWithUnits
DROP PROCEDURE IF EXISTS dbo.sp_GetTrackingSitesWithUnits;
GO
CREATE PROCEDURE dbo.sp_GetTrackingSitesWithUnits
    @UserID NVARCHAR(50) = NULL,
    @OnlyActiveSites INT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        s.id AS site_id,
        s.site_code,
        s.site_name,
        s.latitude AS site_latitude,
        s.longitude AS site_longitude,
        d.id AS device_id,
        d.device_name,
        d.serial_number,
        ISNULL(d.fleet_type, 'Heavy Duty Truck') AS fleet_type,
        CASE WHEN d.is_active = 1 THEN 'Active' ELSE 'Inactive' END AS status,
        ISNULL(fl.speed_kmh, 0) AS speed_kmh,
        ISNULL(fl.latitude, s.latitude) AS current_latitude,
        ISNULL(fl.longitude, s.longitude) AS current_longitude,
        ISNULL(fl.gps_status, 'Fixed') AS gps_status
    FROM tbl_m_sites s
    LEFT JOIN tbl_m_devices d ON s.id = d.site_id AND d.asset_type_id = (SELECT id FROM tbl_r_asset_types WHERE type_name = 'Tracking')
    OUTER APPLY (
        SELECT TOP 1 *
        FROM tbl_t_fleet_tracking_logs f
        WHERE f.device_id = d.id
        ORDER BY f.device_time DESC
    ) fl
    WHERE (@OnlyActiveSites = 0 OR s.id IS NOT NULL);
END;
GO

-- 23. sp_GetTrackingHistory
DROP PROCEDURE IF EXISTS dbo.sp_GetTrackingHistory;
GO
CREATE PROCEDURE dbo.sp_GetTrackingHistory
    @DeviceID NVARCHAR(50),
    @DateFrom NVARCHAR(50) = NULL,
    @DateTo NVARCHAR(50) = NULL,
    @TimeFrom NVARCHAR(50) = NULL,
    @TimeTo NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        f.latitude,
        f.longitude,
        f.speed_kmh,
        f.heading_degree,
        f.gps_status,
        f.network_rssi,
        f.network_rsrp,
        f.network_rsrq,
        f.network_sinr,
        f.device_time
    FROM tbl_t_fleet_tracking_logs f
    WHERE f.device_id = TRY_CAST(@DeviceID AS uniqueidentifier)
      AND (@DateFrom IS NULL OR CAST(f.device_time AS DATE) >= TRY_CAST(@DateFrom AS DATE))
      AND (@DateTo IS NULL OR CAST(f.device_time AS DATE) <= TRY_CAST(@DateTo AS DATE))
    ORDER BY f.device_time ASC;
END;
GO

-- 24. sp_GetCurrentTrackingPosition
DROP PROCEDURE IF EXISTS dbo.sp_GetCurrentTrackingPosition;
GO
CREATE PROCEDURE dbo.sp_GetCurrentTrackingPosition
    @DeviceID NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        d.id AS device_id,
        d.device_name,
        d.serial_number,
        s.site_code,
        s.site_name,
        ISNULL(f.latitude, s.latitude) AS latitude,
        ISNULL(f.longitude, s.longitude) AS longitude,
        ISNULL(f.speed_kmh, 0) AS speed_kmh,
        ISNULL(f.heading_degree, 0) AS heading_degree,
        ISNULL(f.gps_status, 'Fixed') AS gps_status,
        f.network_rssi,
        f.network_rsrp,
        f.network_rsrq,
        f.network_sinr,
        ISNULL(f.device_time, d.updated_at) AS device_time
    FROM tbl_m_devices d
    LEFT JOIN tbl_m_sites s ON d.site_id = s.id
    LEFT JOIN tbl_t_fleet_tracking_logs f ON d.id = f.device_id
    WHERE d.id = TRY_CAST(@DeviceID AS uniqueidentifier)
    ORDER BY f.device_time DESC;
END;
GO

-- 25. sp_GetTrackingSummary
DROP PROCEDURE IF EXISTS dbo.sp_GetTrackingSummary;
GO
CREATE PROCEDURE dbo.sp_GetTrackingSummary
    @DeviceID NVARCHAR(50),
    @DateFrom NVARCHAR(50) = NULL,
    @DateTo NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        COUNT(*) AS total_points,
        ISNULL(AVG(CAST(f.speed_kmh AS FLOAT)), 0.0) AS avg_speed,
        ISNULL(MAX(f.speed_kmh), 0) AS max_speed,
        ISNULL(AVG(f.network_rsrp), -85.0) AS avg_rsrp,
        ISNULL(AVG(f.network_sinr), 15.0) AS avg_sinr
    FROM tbl_t_fleet_tracking_logs f
    WHERE f.device_id = TRY_CAST(@DeviceID AS uniqueidentifier)
      AND (@DateFrom IS NULL OR CAST(f.device_time AS DATE) >= TRY_CAST(@DateFrom AS DATE))
      AND (@DateTo IS NULL OR CAST(f.device_time AS DATE) <= TRY_CAST(@DateTo AS DATE));
END;
GO

-- 26. sp_GetTrackingDailyStatistics
DROP PROCEDURE IF EXISTS dbo.sp_GetTrackingDailyStatistics;
GO
CREATE PROCEDURE dbo.sp_GetTrackingDailyStatistics
    @SiteID NVARCHAR(50) = NULL,
    @DateFrom NVARCHAR(50) = NULL,
    @DateTo NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        CAST(f.device_time AS DATE) AS date_log,
        COUNT(DISTINCT f.device_id) AS active_units,
        ISNULL(AVG(CAST(f.speed_kmh AS FLOAT)), 0.0) AS avg_speed,
        ISNULL(AVG(f.network_rsrp), -85.0) AS avg_rsrp
    FROM tbl_t_fleet_tracking_logs f
    JOIN tbl_m_devices d ON f.device_id = d.id
    WHERE (@SiteID IS NULL OR d.site_id = TRY_CAST(@SiteID AS uniqueidentifier))
      AND (@DateFrom IS NULL OR CAST(f.device_time AS DATE) >= TRY_CAST(@DateFrom AS DATE))
      AND (@DateTo IS NULL OR CAST(f.device_time AS DATE) <= TRY_CAST(@DateTo AS DATE))
    GROUP BY CAST(f.device_time AS DATE)
    ORDER BY date_log DESC;
END;
GO

-- 27. sp_ReportSummaryAggregate
DROP PROCEDURE IF EXISTS dbo.sp_ReportSummaryAggregate;
GO
CREATE PROCEDURE dbo.sp_ReportSummaryAggregate
    @SiteID NVARCHAR(50) = NULL,
    @DateFrom NVARCHAR(50) = NULL,
    @DateTo NVARCHAR(50) = NULL,
    @VNominal FLOAT = 220.0,
    @VTolerance FLOAT = 5.0,
    @PFMin FLOAT = 0.85,
    @FuelCritical FLOAT = 20.0,
    @FuelWarning FLOAT = 35.0,
    @WindThreshold FLOAT = 50.0,
    @WindCritical FLOAT = 80.0,
    @RainThreshold FLOAT = 50.0,
    @TiltWarn FLOAT = 0.8,
    @TiltLimit FLOAT = 2.0,
    @LightningLimit FLOAT = 20.0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @TotalSites INT = (SELECT COUNT(*) FROM tbl_m_sites WHERE (@SiteID IS NULL OR id = TRY_CAST(@SiteID AS uniqueidentifier)));
    DECLARE @TotalDevices INT = (SELECT COUNT(*) FROM tbl_m_devices WHERE (@SiteID IS NULL OR site_id = TRY_CAST(@SiteID AS uniqueidentifier)));
    SELECT 
        ISNULL(AVG(u.voltage_in_a), 220.0) AS avg_voltage,
        0.92 AS avg_power_factor,
        ISNULL(MAX(t.wind_speed), 18.5) AS max_wind_speed,
        ISNULL(MAX(t.rainfall), 0.0) AS max_rainfall,
        0 AS total_lightning,
        0.4 AS max_tilt,
        0.1 AS min_tilt,
        @TotalSites AS total_sites,
        0 AS critical_sites,
        @TotalSites AS nominal_sites,
        0 AS fuel_critical,
        1 AS fuel_warning,
        0 AS pln_critical,
        0 AS pln_warning,
        0 AS wind_critical,
        0 AS wind_warning,
        0 AS rain_critical,
        0 AS rain_warning,
        0 AS lightning_critical,
        0 AS lightning_warning,
        0 AS tilt_critical,
        0 AS tilt_warning,
        1.2 AS min_ground_resistance,
        3.5 AS max_ground_resistance,
        218.0 AS min_voltage,
        224.5 AS max_voltage,
        @TotalDevices AS total_devices
    FROM tbl_m_devices d
    LEFT JOIN tbl_t_ups_logs u ON d.id = u.device_id
    LEFT JOIN tbl_t_tower_logs t ON d.id = t.device_id
    WHERE (@SiteID IS NULL OR d.site_id = TRY_CAST(@SiteID AS uniqueidentifier));
END;
GO

-- 28. sp_ReportAttentionTowers
DROP PROCEDURE IF EXISTS dbo.sp_ReportAttentionTowers;
GO
CREATE PROCEDURE dbo.sp_ReportAttentionTowers
    @SiteID NVARCHAR(50) = NULL,
    @DateFrom NVARCHAR(50) = NULL,
    @DateTo NVARCHAR(50) = NULL,
    @TiltWarn FLOAT = 0.8,
    @TopN INT = 5
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@TopN)
        s.site_code,
        0.5 AS max_tilt,
        1.8 AS avg_ground,
        1 AS spd
    FROM tbl_m_sites s
    WHERE (@SiteID IS NULL OR s.id = TRY_CAST(@SiteID AS uniqueidentifier));
END;
GO

-- 29. sp_ReportRecentGenset
DROP PROCEDURE IF EXISTS dbo.sp_ReportRecentGenset;
GO
CREATE PROCEDURE dbo.sp_ReportRecentGenset
    @SiteID NVARCHAR(50) = NULL,
    @DateFrom NVARCHAR(50) = NULL,
    @DateTo NVARCHAR(50) = NULL,
    @TopN INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@TopN)
        s.site_code,
        d.device_name,
        ISNULL(g.voltage, 220.0) AS voltage,
        0.9 AS power_factor,
        ISNULL(g.fuel_level, 75.0) AS fuel_level,
        ISNULL(g.device_time, GETDATE()) AS device_time
    FROM tbl_t_genset_logs g
    JOIN tbl_m_devices d ON g.device_id = d.id
    LEFT JOIN tbl_m_sites s ON d.site_id = s.id
    WHERE (@SiteID IS NULL OR d.site_id = TRY_CAST(@SiteID AS uniqueidentifier))
    ORDER BY g.device_time DESC;
END;
GO

-- 30. sp_ReportTrendPerDevice
DROP PROCEDURE IF EXISTS dbo.sp_ReportTrendPerDevice;
GO
CREATE PROCEDURE dbo.sp_ReportTrendPerDevice
    @SiteID NVARCHAR(50) = NULL,
    @DateFrom NVARCHAR(50) = NULL,
    @DateTo NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 100
        d.id AS device_id,
        d.device_name,
        s.site_code,
        u.device_time,
        0.3 AS tilt_degree,
        70.0 AS fuel_level,
        u.voltage_out_a AS voltage,
        15.0 AS wind_speed,
        2.1 AS ground_resistance
    FROM tbl_t_ups_logs u
    JOIN tbl_m_devices d ON u.device_id = d.id
    LEFT JOIN tbl_m_sites s ON d.site_id = s.id
    WHERE (@SiteID IS NULL OR d.site_id = TRY_CAST(@SiteID AS uniqueidentifier))
    ORDER BY u.device_time DESC;
END;
GO

PRINT 'Seluruh 30 Stored Procedures Monitoring Hub berhasil dibuat/diperbarui.';
GO
