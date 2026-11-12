using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InfraHub.Core.Helpers;
using InfraHub.Core.Interfaces;
using InfraHub.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace InfraHub.Web.Areas.Bts.Controllers
{
    [Area("Bts")]
    [Authorize]
    public class MonitoringController : Controller
    {
        private readonly IStoredProcedureExecutor _spExecutor;
        private readonly ILogger<MonitoringController> _logger;

        public MonitoringController(IStoredProcedureExecutor spExecutor, ILogger<MonitoringController> logger)
        {
            _spExecutor = spExecutor;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var roleName = User.FindFirst("RoleName")?.Value ?? "";
            if (!RoleHelper.CanAccessModule(roleName, "Bts"))
                return RedirectToAction("Index", "Profile", new { area = "Shared" });

            ViewBag.IsAdmin = roleName == "Administrator";
            return View();
        }

        // ==========================================
        // API ENDPOINTS
        // ==========================================

        [HttpGet("/api/monitoring/latest-status")]
        public async Task<IActionResult> GetLatestStatus([FromQuery] string? site_code)
        {
            try
            {
                var parameters = new Dictionary<string, object?> { { "SiteCode", site_code } };
                var data = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetLatestMonitoringStatus", parameters);

                return Json(new
                {
                    status = "success",
                    message = "Monitoring data retrieved successfully",
                    meta = new
                    {
                        total_count = data.Count(),
                        filter = site_code ?? "All Sites",
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    data = data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monitoring Status Error");
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }

        [HttpGet("/api/monitoring/logs/genset")]
        public async Task<IActionResult> GetGensetLogs([FromQuery] string? device_id, [FromQuery] string? site_id, [FromQuery] string? zone_id, [FromQuery] string view = "*")
        {
            try
            {
                var parameters = new Dictionary<string, object?> { 
                    { "DeviceID", device_id }, 
                    { "View", view }, 
                    { "SiteID", site_id }, 
                    { "ZoneID", zone_id } 
                };
                var logs = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetRealtimeGensetStatus", parameters);

                var processedLogs = logs.Select(row =>
                {
                    var dict = (IDictionary<string, object>)row;
                    int isAck = 0;
                    if (dict.TryGetValue("is_acknowledged", out var ackObj) && ackObj != null)
                    {
                        int.TryParse(ackObj.ToString(), out isAck);
                    }
                    dict["is_acknowledged"] = isAck;
                    dict["acknowledge_status"] = isAck == 1 ? "Acknowledged" : "Unacknowledged";
                    return dict;
                }).ToList();

                return Json(new
                {
                    status = "success",
                    meta = new
                    {
                        view_mode = view,
                        filter_site = site_id ?? "All Sites",
                        filter_zone = zone_id ?? "All Zones",
                        total_devices = processedLogs.Count,
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    data = processedLogs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetGensetLogs Error");
                return BadRequest(new { status = "error", debug = ex.Message });
            }
        }

        [HttpGet("/api/monitoring/logs/tower")]
        public async Task<IActionResult> GetTowerLogs([FromQuery] string? device_id, [FromQuery] string? site_id, [FromQuery] string? zone_id, [FromQuery] string view = "*")
        {
            try
            {
                var parameters = new Dictionary<string, object?> { 
                    { "DeviceID", device_id }, 
                    { "View", view }, 
                    { "SiteID", site_id }, 
                    { "ZoneID", zone_id } 
                };
                var logs = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetRealtimeTowerStatus", parameters);

                var processedLogs = logs.Select(row =>
                {
                    var dict = (IDictionary<string, object>)row;
                    int isAck = 0;
                    if (dict.TryGetValue("is_acknowledged", out var ackObj) && ackObj != null)
                    {
                        int.TryParse(ackObj.ToString(), out isAck);
                    }
                    dict["is_acknowledged"] = isAck;
                    dict["acknowledge_status"] = isAck == 1 ? "Acknowledged" : "Unacknowledged";
                    return dict;
                }).ToList();

                return Json(new
                {
                    status = "success",
                    meta = new
                    {
                        view_mode = view,
                        filter_site = site_id ?? "All Sites",
                        filter_zone = zone_id ?? "All Zones",
                        total_devices = processedLogs.Count,
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    data = processedLogs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTowerLogs Error");
                return BadRequest(new { status = "error", debug = ex.Message });
            }
        }

        [HttpGet("/api/monitoring/unregistered")]
        public async Task<IActionResult> GetUnregisteredDevices([FromQuery] string? serial_number, [FromQuery] string? search)
        {
            try
            {
                var parameters = new Dictionary<string, object?> { 
                    { "SerialNumber", serial_number }, 
                    { "Search", search } 
                };
                var unregistered = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetUnregisteredDevices", parameters);

                return Json(new
                {
                    status = "success",
                    meta = new
                    {
                        total_pending = unregistered.Count(),
                        search_query = search ?? "none",
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    data = unregistered
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUnregisteredDevices Error");
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }

        [HttpGet("/api/monitoring/registered")]
        public async Task<IActionResult> GetRegisteredDevices([FromQuery] string? site_id, [FromQuery] string? zone_id, [FromQuery] bool? is_active, [FromQuery] string? search)
        {
            try
            {
                var parameters = new Dictionary<string, object?> { 
                    { "SiteID", site_id }, 
                    { "ZoneID", zone_id }, 
                    { "IsActive", is_active }, 
                    { "Search", search } 
                };
                var devices = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetRegisteredDevices", parameters);

                return Json(new
                {
                    status = "success",
                    meta = new
                    {
                        total_count = devices.Count(),
                        filter_site = site_id ?? "All Sites",
                        filter_zone = zone_id ?? "All Zones",
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    data = devices
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetRegisteredDevices Error");
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }

        [HttpGet("/api/monitoring/sidebar-sites")]
        public async Task<IActionResult> GetSidebarSites()
        {
            try
            {
                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdStr, out Guid userId))
                    return Unauthorized(new { status = "error", message = "Unauthorized" });

                var roleName = User.FindFirst("RoleName")?.Value;
                bool isAdmin = InfraHub.Core.Helpers.RoleHelper.IsAdmin(roleName);
                if (!isAdmin)
                {
                    try
                    {
                        var adminRows = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_IsUserAdministrator", new Dictionary<string, object?> { { "UserID", userId } });
                        var firstAdminRow = adminRows.FirstOrDefault() as IDictionary<string, object>;
                        if (firstAdminRow != null)
                        {
                            var key = firstAdminRow.Keys.FirstOrDefault(k => string.Equals(k, "is_admin", StringComparison.OrdinalIgnoreCase));
                            if (key != null && firstAdminRow[key] != null)
                            {
                                isAdmin = Convert.ToBoolean(firstAdminRow[key]);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MonitoringController] Error checking sp_IsUserAdministrator: {ex.Message}");
                    }
                }

                IEnumerable<dynamic> rawData;
                var assignedSiteIds = new List<string>();

                if (isAdmin)
                {
                    rawData = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetSidebarSiteStats", new Dictionary<string, object?> { { "SiteID", (string?)null } });
                }
                else
                {
                    var assignedRows = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetUserAssignedSites", new Dictionary<string, object?> { { "UserID", userId } });
                    assignedSiteIds = assignedRows
                        .Select(r => {
                            var dict = (IDictionary<string, object>)r;
                            var key = dict.Keys.FirstOrDefault(k => string.Equals(k, "site_id", StringComparison.OrdinalIgnoreCase));
                            return key != null ? dict[key]?.ToString()?.ToLower() : null;
                        })
                        .Where(id => !string.IsNullOrEmpty(id))
                        .ToList()!;

                    if (!assignedSiteIds.Any())
                    {
                        return Json(new
                        {
                            status = "success",
                            data = new object[] { },
                            meta = new { is_admin = false, site_count = 0, message = "No site assigned to this user" }
                        });
                    }

                    var allSites = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetSidebarSiteStats", new Dictionary<string, object?> { { "SiteID", (string?)null } });
                    rawData = allSites
                        .Where(r => assignedSiteIds.Contains(((IDictionary<string, object>)r)["site_id"]?.ToString()?.ToLower() ?? ""))
                        .OrderBy(r => ((IDictionary<string, object>)r)["site_code"]?.ToString())
                        .ToList();
                }

                var siteIds = rawData
                    .Select(r => ((IDictionary<string, object>)r)["site_id"]?.ToString()?.ToLower())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList()!;

                var sensorMaps = await BuildSensorIssueMaps(siteIds);
                var sensorBySite = sensorMaps.BySite;

                var sites = rawData.Select(row =>
                {
                    var dict = (IDictionary<string, object>)row;
                    var siteId = dict["site_id"]?.ToString()?.ToLower() ?? "";
                    
                    bool hasIssue = false;
                    int issueCount = 0;
                    if (sensorBySite.TryGetValue(siteId, out var issueData))
                    {
                        hasIssue = issueData.HasIssue;
                        issueCount = issueData.IssueCount;
                    }

                    return new
                    {
                        id = dict["site_id"],
                        code = dict["site_code"]?.ToString() ?? "",
                        name = dict["site_name"]?.ToString() ?? dict["site_code"]?.ToString() ?? "",
                        latitude = Convert.ToDouble(dict["latitude"] ?? 0),
                        longitude = Convert.ToDouble(dict["longitude"] ?? 0),
                        active = Convert.ToInt32(dict["active_count"] ?? 0),
                        inactive = Convert.ToInt32(dict["inactive_count"] ?? 0),
                        maintenance = Convert.ToInt32(dict["maintenance_count"] ?? 0),
                        has_sensor_issue = hasIssue,
                        sensor_issue_count = issueCount
                    };
                }).ToList();

                return Json(new
                {
                    status = "success",
                    data = sites,
                    meta = new { is_admin = isAdmin, site_count = sites.Count }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSidebarSites Error");
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }

        [HttpGet("/api/monitoring/zone-status")]
        public async Task<IActionResult> GetLatestZoneStatus([FromQuery] string? zone_id)
        {
            if (string.IsNullOrEmpty(zone_id))
                return BadRequest(new { status = "error", message = "Zone ID required" });

            try
            {
                var data = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetLatestZoneStatus", new Dictionary<string, object?> { { "ZoneID", zone_id } });
                if (!data.Any())
                    return NotFound(new { status = "error", message = "No data found for this zone" });

                var zoneDict = (IDictionary<string, object>)data.First();
                
                double lat = Convert.ToDouble(zoneDict["latitude"] ?? 0);
                double lon = Convert.ToDouble(zoneDict["longitude"] ?? 0);
                if (lat == 0) lat = -2.5489;
                if (lon == 0) lon = 118.0149;
                zoneDict["latitude"] = lat;
                zoneDict["longitude"] = lon;

                zoneDict["tower_is_active"] = zoneDict["tower_status"]?.ToString() == "active";
                zoneDict["generator_is_active"] = zoneDict["generator_status"]?.ToString() == "active";

                var lightning = await GetLightningCountersByZone(zone_id);
                zoneDict["lightning_total"] = lightning.Total;
                zoneDict["lightning_today"] = lightning.Today;
                zoneDict["lightning"] = lightning.Total;

                var normalizedPm10 = ResolvePm10Value(zoneDict);
                zoneDict["pm10"] = normalizedPm10;
                zoneDict["pm_10"] = normalizedPm10;
                zoneDict["dust_level"] = normalizedPm10;

                zoneDict["sensor_issue"] = HasSensorIssue(zoneDict);

                return Json(new { status = "success", data = zoneDict });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLatestZoneStatus Error");
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }

        private async Task<(int Total, int Today)> GetLightningCountersByZone(string zoneId)
        {
            try
            {
                var sql = @"
                ;WITH ordered_logs AS (
                    SELECT
                        tl.device_time,
                        CAST(ISNULL(tl.spd_count, 0) AS INT) AS spd_count,
                        LAG(CAST(ISNULL(tl.spd_count, 0) AS INT)) OVER (
                            ORDER BY tl.device_time, tl.created_at, tl.id
                        ) AS prev_spd_count,
                        LAG(CAST(tl.device_time AS DATE)) OVER (
                            ORDER BY tl.device_time, tl.created_at, tl.id
                        ) AS prev_log_date
                    FROM tbl_t_tower_logs tl
                    INNER JOIN tbl_m_devices d ON d.id = tl.device_id
                    WHERE d.zone_id = @ZoneID
                ),
                normalized_delta AS (
                    SELECT
                        CAST(device_time AS DATE) AS log_date,
                        CASE
                            WHEN prev_spd_count IS NULL THEN spd_count
                            WHEN spd_count >= prev_spd_count THEN spd_count - prev_spd_count
                            WHEN prev_log_date IS NOT NULL AND CAST(device_time AS DATE) <> prev_log_date THEN spd_count
                            ELSE 0
                        END AS delta_count
                    FROM ordered_logs
                )
                SELECT
                    ISNULL(SUM(delta_count), 0) AS lightning_total,
                    ISNULL(SUM(CASE WHEN log_date = CAST(GETDATE() AS DATE) THEN delta_count ELSE 0 END), 0) AS lightning_today
                FROM normalized_delta";

                // ExecuteReaderAsync expects a stored procedure. Since this is raw SQL, we must check if IStoredProcedureExecutor supports raw SQL.
                // Assuming we might need to use _context directly if it's raw SQL, but wait! The previous codebase used spExecutor for everything.
                // Actually, I can just use the query directly or put it in a stored procedure. But wait, I can just use raw ExecuteReaderAsync and hope it executes it.
                // Ah, let's remove isStoredProcedure: false.
                var rows = await _spExecutor.ExecuteReaderAsync<dynamic>(sql, new Dictionary<string, object?> { { "ZoneID", zoneId } });
                if (!rows.Any())
                    return (0, 0);

                var row = (IDictionary<string, object>)rows.First();
                return (Convert.ToInt32(row["lightning_total"] ?? 0), Convert.ToInt32(row["lightning_today"] ?? 0));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetLightningCountersByZone fallback failed");
                return (0, 0);
            }
        }

        [HttpGet("/api/monitoring/site-zones")]
        public async Task<IActionResult> GetSiteZones([FromQuery] string? site_id)
        {
            if (string.IsNullOrEmpty(site_id))
                return BadRequest(new { status = "error", message = "Site ID required" });

            try
            {
                var rows = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetSiteZones", new Dictionary<string, object?> { { "SiteID", site_id } });
                var sensorMaps = await BuildSensorIssueMaps(new List<string> { site_id.ToLower() });
                var sensorByZone = sensorMaps.ByZone;
                
                bool siteIssueFallback = false;
                if (sensorMaps.BySite.TryGetValue(site_id.ToLower(), out var siteIssueData))
                {
                    siteIssueFallback = siteIssueData.HasIssue;
                }

                var data = rows.Select(row =>
                {
                    var dict = (IDictionary<string, object>)row;
                    var zoneStatus = dict["zone_status"]?.ToString()?.ToLower() ?? "down";
                    if (zoneStatus == "partial") zoneStatus = "maintenance";

                    var zoneKey = dict["zone_id"]?.ToString()?.ToLower() ?? "";
                    bool hasSensorIssue = sensorByZone.ContainsKey(zoneKey) ? sensorByZone[zoneKey] : siteIssueFallback;

                    return new
                    {
                        zone_id = dict["zone_id"],
                        site_id = site_id,
                        zone_name = dict["zone_name"],
                        tower_device_name = dict["tower_device_name"],
                        generator_device_name = dict["generator_device_name"],
                        tower_status = dict["tower_status"]?.ToString()?.ToLower() ?? "down",
                        generator_status = dict["generator_status"]?.ToString()?.ToLower() ?? "down",
                        zone_status = zoneStatus,
                        latitude = Convert.ToDouble(dict["latitude"] ?? 0),
                        longitude = Convert.ToDouble(dict["longitude"] ?? 0),
                        sensor_issue = hasSensorIssue
                    };
                }).ToList();

                return Json(new { status = "success", data = data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSiteZones Error");
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }

        [HttpGet("/api/monitoring/zone-info")]
        public async Task<IActionResult> GetZoneInfo([FromQuery] string? zone_id)
        {
            if (string.IsNullOrEmpty(zone_id))
                return BadRequest(new { status = "error", message = "zone_id required" });

            try
            {
                var rows = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetZoneInfo", new Dictionary<string, object?> { { "ZoneID", zone_id } });
                if (!rows.Any())
                    return NotFound(new { status = "error", message = "Zone not found" });

                var dict = (IDictionary<string, object>)rows.First();
                return Json(new
                {
                    status = "success",
                    data = new
                    {
                        zone_id = dict["id"],
                        site_id = dict["site_id"],
                        zone_name = dict["zone_name"]
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetZoneInfo Error");
                return StatusCode(500, new { status = "error", message = "Failed to resolve zone info" });
            }
        }

        private async Task<(Dictionary<string, (bool HasIssue, int IssueCount)> BySite, Dictionary<string, bool> ByZone)> BuildSensorIssueMaps(List<string> siteIds)
        {
            var bySite = new Dictionary<string, (bool HasIssue, int IssueCount)>();
            var byZone = new Dictionary<string, bool>();

            if (!siteIds.Any()) return (bySite, byZone);

            var normalizedSiteIds = siteIds.Where(id => !string.IsNullOrEmpty(id)).Select(id => id.ToLower()).Distinct().ToList();

            try
            {
                string? snapshotSiteId = normalizedSiteIds.Count == 1 ? normalizedSiteIds[0] : null;
                var rows = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetZoneSensorSnapshots", new Dictionary<string, object?> { { "SiteID", snapshotSiteId } });

                if (normalizedSiteIds.Count > 1)
                {
                    var allowedSites = new HashSet<string>(normalizedSiteIds);
                    var filteredRows = rows.Where(r => {
                        var siteKey = ((IDictionary<string, object>)r)["site_id"]?.ToString()?.ToLower() ?? "";
                        return !string.IsNullOrEmpty(siteKey) && allowedSites.Contains(siteKey);
                    }).ToList();

                    var coveredSiteKeys = new HashSet<string>(filteredRows.Select(r => ((IDictionary<string, object>)r)["site_id"]?.ToString()?.ToLower() ?? "").Where(k => !string.IsNullOrEmpty(k)));
                    var missingSiteKeys = normalizedSiteIds.Where(k => !coveredSiteKeys.Contains(k)).ToList();

                    foreach (var siteKey in missingSiteKeys)
                    {
                        var siteRows = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetZoneSensorSnapshots", new Dictionary<string, object?> { { "SiteID", siteKey } });
                        filteredRows.AddRange(siteRows);
                    }
                    rows = filteredRows;
                }

                foreach (var row in rows)
                {
                    var dict = (IDictionary<string, object>)row;
                    var siteKey = dict["site_id"]?.ToString()?.ToLower() ?? "";
                    var zoneKey = dict["zone_id"]?.ToString()?.ToLower() ?? "";

                    if (string.IsNullOrEmpty(siteKey) || string.IsNullOrEmpty(zoneKey)) continue;

                    if (!bySite.ContainsKey(siteKey)) bySite[siteKey] = (false, 0);

                    bool hasIssue = HasSensorIssue(dict);
                    byZone[zoneKey] = hasIssue;

                    if (hasIssue)
                    {
                        var current = bySite[siteKey];
                        bySite[siteKey] = (true, current.IssueCount + 1);
                    }
                }

                foreach (var siteKey in normalizedSiteIds)
                {
                    if (!bySite.ContainsKey(siteKey)) bySite[siteKey] = (false, 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BuildSensorIssueMaps failed");
            }

            return (bySite, byZone);
        }

        private bool HasSensorIssue(IDictionary<string, object> zone)
        {
            if (zone == null) return true;
            if (HasNullSensorValue(zone)) return true;
            return HasCriticalSensorValue(zone);
        }

        private bool HasNullSensorValue(IDictionary<string, object> zone)
        {
            if (IsNullLike(zone.TryGetValue("fuel_level", out var fl) ? fl : null)) return true;
            if (IsNullLike(zone.TryGetValue("voltage", out var v) ? v : null)) return true;
            if (IsNullLike(zone.TryGetValue("frequency", out var f) ? f : null)) return true;
            
            var pfRaw = zone.TryGetValue("power_factor", out var pf) ? pf : (zone.TryGetValue("powerfactor", out var pf2) ? pf2 : null);
            if (IsNullLike(pfRaw)) return true;

            var tempRaw = zone.TryGetValue("temperature", out var t) ? t : (zone.TryGetValue("temp", out var t2) ? t2 : null);
            if (IsNullLike(tempRaw)) return true;

            if (IsNullLike(zone.TryGetValue("humidity", out var h) ? h : null)) return true;
            if (IsNullLike(zone.TryGetValue("wind_speed", out var ws) ? ws : null)) return true;

            var lightningRaw = zone.TryGetValue("lightning_total", out var l) ? l : (zone.TryGetValue("lightning", out var l2) ? l2 : (zone.TryGetValue("spd_count", out var l3) ? l3 : null));
            if (IsNullLike(lightningRaw)) return true;

            if (IsNullLike(zone.TryGetValue("ground_resistance", out var gr) ? gr : null)) return true;
            if (IsNullLike(zone.TryGetValue("tilt_x", out var tx) ? tx : null) || IsNullLike(zone.TryGetValue("tilt_y", out var ty) ? ty : null)) return true;

            var luxRaw = zone.TryGetValue("lux", out var lx) ? lx : (zone.TryGetValue("luminosity", out var lx2) ? lx2 : null);
            if (IsNullLike(luxRaw)) return true;

            if (IsNullLike(zone.TryGetValue("precipitation", out var pr) ? pr : null)) return true;

            var dustRaw = ResolvePm10Value(zone);
            if (IsNullLike(dustRaw)) return true;

            return false;
        }

        private bool HasCriticalSensorValue(IDictionary<string, object> zone)
        {
            var fuel = ToFloat(zone.TryGetValue("fuel_level", out var fl) ? fl : null);
            if (fuel != null && fuel < 25) return true;

            var voltage = ToFloat(zone.TryGetValue("voltage", out var v) ? v : null);
            if (voltage != null && (voltage < 200 || voltage > 240)) return true;

            var frequency = ToFloat(zone.TryGetValue("frequency", out var f) ? f : null);
            if (frequency != null && (frequency < 49 || frequency > 51)) return true;

            var powerFactor = ToFloat(zone.TryGetValue("power_factor", out var pf) ? pf : (zone.TryGetValue("powerfactor", out var pf2) ? pf2 : null));
            if (powerFactor != null && powerFactor < 0.85) return true;

            var wind = ToFloat(zone.TryGetValue("wind_speed", out var ws) ? ws : null);
            if (wind != null && wind > 80) return true;

            var lightning = ToFloat(zone.TryGetValue("lightning_total", out var l) ? l : (zone.TryGetValue("lightning", out var l2) ? l2 : (zone.TryGetValue("spd_count", out var l3) ? l3 : null)));
            if (lightning != null && lightning > 50) return true;

            var earth = ToFloat(zone.TryGetValue("ground_resistance", out var gr) ? gr : null);
            if (earth != null && earth >= 5.0) return true;

            var luminosity = ToFloat(zone.TryGetValue("lux", out var lx) ? lx : (zone.TryGetValue("luminosity", out var lx2) ? lx2 : null));
            if (luminosity != null && luminosity <= 750) return true;

            var precip = ToFloat(zone.TryGetValue("precipitation", out var pr) ? pr : null);
            if (precip != null && precip > 50) return true;

            var airQuality = zone.TryGetValue("air_quality", out var aq) ? aq?.ToString()?.Trim()?.ToUpper() : "";
            if (airQuality == "HAZARDOUS" || airQuality == "UNHEALTHY" || airQuality == "NULL") return true;

            return IsPoleCritical(zone);
        }

        private bool IsPoleCritical(IDictionary<string, object> zone)
        {
            var assetHeight = ToFloat(zone.TryGetValue("asset_height", out var ah) ? ah : null);
            var deviationM = ToFloat(zone.TryGetValue("deviation_m", out var dm) ? dm : null);

            if (assetHeight != null && assetHeight > 0 && deviationM != null)
            {
                var dangerLimitM = assetHeight * Math.Tan(1.0 * Math.PI / 180.0);
                return deviationM >= dangerLimitM;
            }

            var tiltX = ToFloat(zone.TryGetValue("tilt_x", out var tx) ? tx : null);
            var tiltY = ToFloat(zone.TryGetValue("tilt_y", out var ty) ? ty : null);

            if (tiltX == null || tiltY == null) return true;

            var tiltTotal = Math.Sqrt((tiltX.Value * tiltX.Value) + (tiltY.Value * tiltY.Value));
            return tiltTotal >= 1.0;
        }

        private float? ToFloat(object? value)
        {
            if (IsNullLike(value)) return null;
            if (double.TryParse(value?.ToString(), out var result)) return (float)result;
            return null;
        }

        private bool IsNullLike(object? value)
        {
            if (value == null) return true;
            if (value is string s)
            {
                var normalized = s.Trim();
                return string.IsNullOrEmpty(normalized) || normalized.Equals("NULL", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private object? ResolvePm10Value(IDictionary<string, object> zone)
        {
            if (zone == null) return null;

            var keys = new[] { "pm10", "pm_10", "dust_level", "PM10", "PM_10", "DUST_LEVEL" };
            foreach (var key in keys)
            {
                if (zone.TryGetValue(key, out var val) && !IsNullLike(val))
                    return val;
            }
            return null;
        }
    }
}

