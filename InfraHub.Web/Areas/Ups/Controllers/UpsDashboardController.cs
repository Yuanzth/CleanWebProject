using InfraHub.Core.Interfaces;
using InfraHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Dynamic;

namespace InfraHub.Web.Areas.Ups.Controllers;

[Area("Ups")]
[Authorize]
[Route("[area]/Dashboard")]
public class UpsDashboardController : Controller
{
    private readonly AppDbContext _context;
    private readonly IStoredProcedureExecutor _spExecutor;

    public UpsDashboardController(AppDbContext context, IStoredProcedureExecutor spExecutor)
    {
        _context = context;
        _spExecutor = spExecutor;
    }

    private async Task<List<string>?> GetAuthorizedSiteIdsAsync()
    {
        var roleName = User.FindFirst("RoleName")?.Value;
        if (InfraHub.Core.Helpers.RoleHelper.IsAdmin(roleName))
        {
            return null; // Admin (Administrator or NOC) has all access
        }

        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return new List<string>();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return new List<string>();

        var parameters = new Dictionary<string, object?> { { "UserId", user.Id } };
        
        var assignedResult = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetUserAssignedSites", parameters);
        var siteIds = new List<string>();
        foreach (var row in assignedResult)
        {
            var r = (IDictionary<string, object>)row;
            if (r.ContainsKey("site_id") && r["site_id"] != null)
                siteIds.Add(r["site_id"].ToString()!);
        }

        return siteIds;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var authorizedSiteIds = await GetAuthorizedSiteIdsAsync();
        var isAdmin = authorizedSiteIds == null;
        
        var sites = await FetchSiteRowsAsync(authorizedSiteIds);
        
        var siteCards = BuildSiteCards(sites);

        ViewBag.IsAdmin = isAdmin;
        return View(siteCards);
    }
    
    [HttpGet("/api/ups/dashboard-data")]
    public async Task<IActionResult> GetDashboardData()
    {
        try
        {
            var authorizedSiteIds = await GetAuthorizedSiteIdsAsync();
            var rawSites = await FetchSiteRowsAsync(authorizedSiteIds);
            var siteCards = BuildSiteCards(rawSites);
            
            return Json(new {
                status = "success",
                message = "Dashboard data retrieved successfully",
                meta = new {
                    total_sites = siteCards.Count,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                },
                data = siteCards
            });
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText("ups_dashboard_error.txt", ex.ToString());
            return StatusCode(500, new {
                status = "error",
                message = "Failed to retrieve dashboard data",
                error = ex.Message
            });
        }
    }

    [AllowAnonymous]
    [HttpGet("/api/ups/dashboard-test")]
    public async Task<IActionResult> GetDashboardTest()
    {
        try
        {
            var rawSites = await FetchSiteRowsAsync(null);
            var siteCards = BuildSiteCards(rawSites);
            
            return Json(new {
                status = "success",
                data = siteCards,
                rawSitesCount = rawSites.Count()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.ToString());
        }
    }

    private async Task<IEnumerable<dynamic>> FetchSiteRowsAsync(List<string>? authorizedSiteIds)
    {
        if (authorizedSiteIds != null && authorizedSiteIds.Count == 0)
        {
            return new List<dynamic>();
        }

        var filters = new List<string>();
        var bindings = new List<object>();

        if (authorizedSiteIds != null)
        {
            var placeholders = string.Join(", ", authorizedSiteIds.Select((_, i) => $"@p{i}"));
            filters.Add($"COALESCE(z.site_id, d.site_id) IN ({placeholders})");
            bindings.AddRange(authorizedSiteIds);
        }

        var whereClause = filters.Any() ? "AND " + string.Join("\n            AND ", filters) : "";

        var sql = $@"
WITH ups_devices AS (
    SELECT
        d.id AS device_id,
        d.device_name,
        d.ip_address,
        d.ups_type,
        d.ups_phase_type,
        COALESCE(d.ups_category, 'UPS DC') AS ups_category,
        COALESCE(z.site_id, d.site_id) AS site_id,
        at.type_name AS asset_type_name
    FROM tbl_m_devices d
    LEFT JOIN tbl_m_zones z ON z.id = d.zone_id
    INNER JOIN tbl_r_asset_types at ON at.id = d.asset_type_id
    WHERE d.is_active = 1
      AND UPPER(at.type_name) IN ('UPS', 'CCTV', 'PAC', 'SENSOR')
      {whereClause}
)
SELECT
    s.id AS site_id,
    s.site_code,
    s.site_name,
    ud.device_id,
    ud.device_name,
    ud.ip_address,
    ud.ups_type,
    ud.ups_phase_type,
    ud.ups_category,
    ud.asset_type_name,
    ll.voltage_in_a,
    ll.voltage_in_b,
    ll.voltage_in_c,
    ll.current_in_a,
    ll.current_in_b,
    ll.current_in_c,
    ll.input_frequency,
    ll.voltage_out_a,
    ll.voltage_out_b,
    ll.voltage_out_c,
    ll.current_out_a,
    ll.current_out_b,
    ll.current_out_c,
    ll.output_frequency,
    ll.load_percent,
    ll.grounding_resistance,
    ll.battery_percent,
    ll.battery_voltage,
    ll.battery_runtime_min,
    ll.ambient_temp,
    ll.room_temp,
    COALESCE(cl.status, CASE WHEN UPPER(ud.asset_type_name) IN ('PAC', 'SENSOR') THEN NULL ELSE ll.ups_status END) AS ups_status,
    (SELECT MAX(v) FROM (VALUES (ll.device_time), (sl.sl_time), (pl.pl_time), (cl.ccl_time)) AS value(v)) AS latest_sample_at,
    ll.device_time AS last_online_time,
    sl.sl_temp,
    sl.sl_humid,
    pl.pl_temp,
    pl.pl_humid,
    pl.pl_temp_setpoint,
    pl.pl_humid_setpoint,
    pl.pl_valve_open,
    pl.pl_water_in_temp,
    pl.pl_water_out_temp,
    pl.pl_cool_demand,
    pl.pl_heat_demand,
    pl.pl_dehum_demand,
    pl.pl_humid_demand,
    pl.pl_unit_id,
    pl.pl_supervisory_status,
    pl.pl_unit_status_code,
    pl.pl_comp1,
    pl.pl_comp2,
    pl.pl_comp3,
    pl.pl_heater1,
    pl.pl_heater2,
    pl.pl_alarms,
    pl.pl_time,
    cl.status AS ccl_status,
    cl.ccl_time,
    ack.ack_id,
    ack.ack_reason,
    ack.ack_notes,
    ack.ack_verification_status,
    ack.ack_start_time,
    ack.ack_end_time,
    ack.ack_verified_at,
    ack.ack_by_name,
    ack.ack_verified_by_name,
    ack.ack_verification_notes,
    GETDATE() AS current_db_time
FROM ups_devices ud
JOIN tbl_m_sites s ON s.id = ud.site_id
OUTER APPLY (
    SELECT TOP 1 u.*
    FROM tbl_t_ups_logs u
    WHERE u.device_id = ud.device_id AND UPPER(ud.asset_type_name) = 'UPS'
    ORDER BY u.device_time DESC
) ll
OUTER APPLY (
    SELECT TOP 1
        sl_inner.device_time AS sl_time,
        sl_inner.temperature AS sl_temp,
        sl_inner.humidity AS sl_humid
    FROM tbl_t_sensor_logs sl_inner
    WHERE sl_inner.device_id = ud.device_id AND UPPER(ud.asset_type_name) = 'SENSOR'
    ORDER BY sl_inner.device_time DESC
) sl
OUTER APPLY (
    SELECT TOP 1
        pl_inner.device_time AS pl_time,
        pl_inner.room_temp AS pl_temp,
        pl_inner.room_humid AS pl_humid,
        pl_inner.temp_setpoint AS pl_temp_setpoint,
        pl_inner.humid_setpoint AS pl_humid_setpoint,
        pl_inner.valve_open AS pl_valve_open,
        pl_inner.water_in_temp AS pl_water_in_temp,
        pl_inner.water_out_temp AS pl_water_out_temp,
        pl_inner.cool_demand AS pl_cool_demand,
        pl_inner.heat_demand AS pl_heat_demand,
        pl_inner.dehum_demand AS pl_dehum_demand,
        pl_inner.humid_demand AS pl_humid_demand,
        pl_inner.unit_id AS pl_unit_id,
        pl_inner.supervisory_status AS pl_supervisory_status,
        pl_inner.unit_status_code AS pl_unit_status_code,
        pl_inner.comp1 AS pl_comp1,
        pl_inner.comp2 AS pl_comp2,
        pl_inner.comp3 AS pl_comp3,
        pl_inner.heater1 AS pl_heater1,
        pl_inner.heater2 AS pl_heater2,
        pl_inner.active_alarms AS pl_alarms
    FROM tbl_t_pac_logs pl_inner
    WHERE pl_inner.device_id = ud.device_id AND UPPER(ud.asset_type_name) = 'PAC'
    ORDER BY pl_inner.device_time DESC
) pl
OUTER APPLY (
    SELECT TOP 1
        cl_inner.device_time AS ccl_time,
        cl_inner.status AS ccl_status,
        cl_inner.status
    FROM tbl_t_cctv_logs cl_inner
    WHERE cl_inner.device_id = ud.device_id AND UPPER(ud.asset_type_name) = 'CCTV'
    ORDER BY cl_inner.device_time DESC
) cl
OUTER APPLY (
    SELECT TOP 1
        ack_inner.id AS ack_id,
        ack_inner.reason AS ack_reason,
        ack_inner.notes AS ack_notes,
        ack_inner.verification_status AS ack_verification_status,
        ack_inner.start_time AS ack_start_time,
        ack_inner.end_time AS ack_end_time,
        ack_inner.verified_at AS ack_verified_at,
        u_ack.name AS ack_by_name,
        u_ver.name AS ack_verified_by_name,
        ack_inner.verification_notes AS ack_verification_notes
    FROM tbl_t_device_acknowledges ack_inner
    LEFT JOIN tbl_m_users u_ack ON u_ack.id = ack_inner.acknowledged_by
    LEFT JOIN tbl_m_users u_ver ON u_ver.id = ack_inner.verified_by
    WHERE LOWER(ack_inner.device_id) = LOWER(CAST(ud.device_id AS NVARCHAR(50)))
    ORDER BY ack_inner.created_at DESC
) ack
ORDER BY s.site_code, s.site_name, ud.device_name";

        return await _spExecutor.ExecuteSqlAsync(sql, bindings.ToArray());
    }

    private List<dynamic> BuildSiteCards(IEnumerable<dynamic> rawSites)
    {
        var sitesDict = new Dictionary<string, ExpandoObject>();

        foreach (var rowObj in rawSites)
        {
            var row = (IDictionary<string, object>)rowObj;
            var siteId = row["site_id"]?.ToString();
            if (string.IsNullOrEmpty(siteId)) continue;

            if (!sitesDict.ContainsKey(siteId))
            {
                dynamic newSite = new ExpandoObject();
                newSite.site_id = siteId;
                newSite.site_code = row["site_code"]?.ToString() ?? "";
                newSite.site_name = row["site_name"]?.ToString() ?? "";
                newSite.status = "normal";
                newSite.categories = new ExpandoObject();
                sitesDict[siteId] = newSite;
            }

            var site = (IDictionary<string, object>)sitesDict[siteId];
            var categories = (IDictionary<string, object>)site["categories"];
            
            var rawCat = row.ContainsKey("ups_category") && row["ups_category"] != null ? row["ups_category"].ToString() : "UPS DC";
            var catName = string.IsNullOrEmpty(rawCat) ? "UPS DC" : rawCat.Trim().ToUpper();
            
            if (!categories.ContainsKey(catName))
            {
                dynamic newCat = new ExpandoObject();
                newCat.status = "normal";
                newCat.has_dead_device = false;
                newCat.devices = new List<dynamic>();
                categories[catName] = newCat;
            }

            var cat = (IDictionary<string, object>)categories[catName];
            var devices = (List<dynamic>)cat["devices"];
            
            // Initialize anomaly tracking flags
            if (!cat.ContainsKey("has_temp_issue")) cat["has_temp_issue"] = false;
            if (!cat.ContainsKey("has_voltage_issue")) cat["has_voltage_issue"] = false;
            if (!cat.ContainsKey("has_freq_issue")) cat["has_freq_issue"] = false;
            if (!cat.ContainsKey("has_load_issue")) cat["has_load_issue"] = false;
            if (!cat.ContainsKey("has_battery_issue")) cat["has_battery_issue"] = false;
            if (!cat.ContainsKey("has_ack_device")) cat["has_ack_device"] = false;
            
            // Porting Laravel's anomaly & stale data logic
            var latestSampleAt = row.ContainsKey("latest_sample_at") && row["latest_sample_at"] != null ? (DateTime?)row["latest_sample_at"] : null;
            var upsStatus = row.ContainsKey("ups_status") && row["ups_status"] != null ? row["ups_status"].ToString()?.Trim().ToLower() : "";
            
            var assetTypeName = row.ContainsKey("asset_type_name") && row["asset_type_name"] != null ? row["asset_type_name"].ToString()?.Trim().ToUpper() : "";
            bool isCctv = assetTypeName == "CCTV" || catName.Contains("CCTV");
            bool isPac = assetTypeName == "PAC" || catName.Contains("PAC");
            bool isSensor = assetTypeName == "SENSOR" || catName.Contains("SENSOR");
            bool isNonUpsDevice = isCctv || isPac || isSensor;
            
            bool isExplicitOffline = upsStatus == "offline" || upsStatus == "connection error" || upsStatus == "auth error" || upsStatus == "system error" || upsStatus == "stream error" || upsStatus == "down";
            bool isStale = false;

            var currentDbTime = row.ContainsKey("current_db_time") && row["current_db_time"] != null ? Convert.ToDateTime(row["current_db_time"]) : DateTime.Now;

            // Acknowledge logic
            var ackId = row.ContainsKey("ack_id") && row["ack_id"] != null ? row["ack_id"].ToString() : null;
            var ackReason = row.ContainsKey("ack_reason") && row["ack_reason"] != null ? row["ack_reason"].ToString() : null;
            var ackNotes = row.ContainsKey("ack_notes") && row["ack_notes"] != null ? row["ack_notes"].ToString() : null;
            var ackVerStatus = row.ContainsKey("ack_verification_status") && row["ack_verification_status"] != null ? row["ack_verification_status"].ToString() : null;
            var ackStartTime = row.ContainsKey("ack_start_time") && row["ack_start_time"] != null ? (DateTime?)Convert.ToDateTime(row["ack_start_time"]) : null;
            var ackEndTime = row.ContainsKey("ack_end_time") && row["ack_end_time"] != null ? (DateTime?)Convert.ToDateTime(row["ack_end_time"]) : null;
            var ackByName = row.ContainsKey("ack_by_name") && row["ack_by_name"] != null ? row["ack_by_name"].ToString() : null;
            var ackVerByName = row.ContainsKey("ack_verified_by_name") && row["ack_verified_by_name"] != null ? row["ack_verified_by_name"].ToString() : null;
            var ackVerNotes = row.ContainsKey("ack_verification_notes") && row["ack_verification_notes"] != null ? row["ack_verification_notes"].ToString() : null;

            bool isVerifiedAck = string.Equals(ackVerStatus, "Verified", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(ackVerStatus, "Approved", StringComparison.OrdinalIgnoreCase);

            // An acknowledge is active if verified and current time is within or before end_time
            bool isAckActive = false;
            if (isVerifiedAck)
            {
                if (ackEndTime.HasValue)
                {
                    isAckActive = currentDbTime <= ackEndTime.Value;
                }
                else
                {
                    isAckActive = true;
                }
            }

            row["is_acknowledged"] = isAckActive;
            row["ack_status"] = ackVerStatus;
            row["ack_reason"] = ackReason;
            row["ack_notes"] = ackNotes;
            row["ack_start_time"] = ackStartTime?.ToString("yyyy-MM-dd HH:mm:ss");
            row["ack_end_time"] = ackEndTime?.ToString("yyyy-MM-dd HH:mm:ss");
            row["ack_by_name"] = ackByName;
            row["ack_verified_by_name"] = ackVerByName;
            row["ack_verification_notes"] = ackVerNotes;

            if (latestSampleAt.HasValue)
            {
                var diffMinutes = currentDbTime.Subtract(latestSampleAt.Value).TotalMinutes;
                if (diffMinutes > 15 && !(diffMinutes >= 405 && diffMinutes <= 435))
                {
                    isStale = true;
                }
            }
            else
            {
                // For non-UPS devices, if there's no log data at all, mark as no-data instead of stale/critical
                if (isNonUpsDevice)
                {
                    isStale = false; // Will be handled by post-processing (empty devices -> no-data)
                }
                else
                {
                    isStale = true;
                }
            }

            bool isDead = isExplicitOffline || isStale;
            bool isBatteryMode = !isNonUpsDevice && (upsStatus == "on battery" || upsStatus == "system on dc source" || upsStatus == "on bypass" || upsStatus == "battery mode");

            string metricStatus = "normal";
            
            if (isExplicitOffline || isStale)
            {
                metricStatus = "critical";
                if (!isAckActive)
                {
                    cat["has_dead_device"] = true;
                    cat["is_stale"] = isStale && !isExplicitOffline;
                }
            }
            else if (isBatteryMode)
            {
                metricStatus = "warning";
                if (!isAckActive)
                {
                    cat["has_battery_issue"] = true;
                }
            }
            
            if (isAckActive)
            {
                row["alarm_suppressed"] = true;
                cat["has_ack_device"] = true;
            }
            
            row["is_offline"] = isExplicitOffline || isStale;
            
            Func<object?, double> ParseDoubleSafe = obj =>
            {
                if (obj == null) return 0;
                string s = obj.ToString()?.Trim().Replace(",", ".") ?? "0";
                return double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double result) ? result : 0;
            };

            // Anomaly Checks (Temperature, Voltage, Frequency, Load, etc)
            double ambientTemp = row.ContainsKey("ambient_temp") ? ParseDoubleSafe(row["ambient_temp"]) : 0;
            double roomTemp = row.ContainsKey("room_temp") ? ParseDoubleSafe(row["room_temp"]) : 0;
            double slTemp = row.ContainsKey("sl_temp") ? ParseDoubleSafe(row["sl_temp"]) : 0;
            double slHumid = row.ContainsKey("sl_humid") ? ParseDoubleSafe(row["sl_humid"]) : 0;
            double plTemp = row.ContainsKey("pl_temp") ? ParseDoubleSafe(row["pl_temp"]) : 0;
            double plHumid = row.ContainsKey("pl_humid") ? ParseDoubleSafe(row["pl_humid"]) : 0;
            double plTempSetpoint = row.ContainsKey("pl_temp_setpoint") ? ParseDoubleSafe(row["pl_temp_setpoint"]) : 0;
            double plHumidSetpoint = row.ContainsKey("pl_humid_setpoint") ? ParseDoubleSafe(row["pl_humid_setpoint"]) : 0;
            string plAlarms = row.ContainsKey("pl_alarms") && row["pl_alarms"] != null ? row["pl_alarms"].ToString() : "";
            
            // Check specific category anomalies
            if (catName.Contains("SENSOR"))
            {
                if (slTemp > 30.0 || (slTemp > 0 && slTemp < 13.0)) {
                    metricStatus = "critical";
                    cat["has_temp_issue"] = true;
                } else if ((slTemp >= 25.1 && slTemp <= 30.0) || (slTemp >= 13.0 && slTemp <= 17.9)) {
                    if (metricStatus != "critical") metricStatus = "warning";
                    cat["has_temp_issue"] = true;
                }
                
                if (slHumid > 65.0 || (slHumid > 0 && slHumid < 35.0)) {
                    metricStatus = "critical";
                    cat["has_temp_issue"] = true;
                } else if ((slHumid >= 60.1 && slHumid <= 65.0) || (slHumid >= 35.0 && slHumid <= 39.9)) {
                    if (metricStatus != "critical") metricStatus = "warning";
                    cat["has_temp_issue"] = true;
                }
            }
            else if (catName.Contains("PAC"))
            {
                if (!string.IsNullOrEmpty(plAlarms) && plAlarms.ToLower() != "normal" && plAlarms.ToLower() != "none") {
                    metricStatus = "critical";
                    cat["has_temp_issue"] = true;
                }

                if (plTempSetpoint > 0) {
                    double tempDiff = Math.Abs(plTemp - plTempSetpoint);
                    if (tempDiff >= 13) {
                        metricStatus = "critical";
                        cat["has_temp_issue"] = true;
                    } else if (tempDiff >= 8) {
                        if (metricStatus != "critical") metricStatus = "warning";
                        cat["has_temp_issue"] = true;
                    }
                } else {
                    if (plTemp > 30.0 || (plTemp > 0 && plTemp < 13.0)) {
                        metricStatus = "critical";
                        cat["has_temp_issue"] = true;
                    } else if ((plTemp >= 25.1 && plTemp <= 30.0) || (plTemp >= 13.0 && plTemp <= 17.9)) {
                        if (metricStatus != "critical") metricStatus = "warning";
                        cat["has_temp_issue"] = true;
                    }
                }

                if (plHumidSetpoint > 0) {
                    double humidDiff = Math.Abs(plHumid - plHumidSetpoint);
                    if (humidDiff >= 13) {
                        metricStatus = "critical";
                        cat["has_temp_issue"] = true;
                    } else if (humidDiff >= 8) {
                        if (metricStatus != "critical") metricStatus = "warning";
                        cat["has_temp_issue"] = true;
                    }
                } else {
                    if (plHumid > 65.0 || (plHumid > 0 && plHumid < 35.0)) {
                        metricStatus = "critical";
                        cat["has_temp_issue"] = true;
                    } else if ((plHumid >= 60.1 && plHumid <= 65.0) || (plHumid >= 35.0 && plHumid <= 39.9)) {
                        if (metricStatus != "critical") metricStatus = "warning";
                        cat["has_temp_issue"] = true;
                    }
                }
            }
            else if (isCctv)
            {
                if (isExplicitOffline || isStale || (!string.IsNullOrEmpty(upsStatus) && upsStatus != "online" && upsStatus != "up"))
                {
                    metricStatus = "critical";
                    cat["has_dead_device"] = true;
                }
            }
            else // UPS
            {
                if (ambientTemp > 35.0 || roomTemp > 35.0) {
                    metricStatus = "critical";
                    cat["has_temp_issue"] = true;
                } else if ((ambientTemp >= 30.0 || roomTemp >= 30.0)) {
                    if (metricStatus != "critical") metricStatus = "warning";
                    cat["has_temp_issue"] = true;
                }
            }

            // Battery Anomaly Checks
            var battPercent = row.ContainsKey("battery_percent") && row["battery_percent"] != null ? Convert.ToDouble(row["battery_percent"]) : (double?)null;
            var upsType = row.ContainsKey("ups_type") && row["ups_type"] != null ? row["ups_type"].ToString() : "";
            var devName = row.ContainsKey("device_name") && row["device_name"] != null ? row["device_name"].ToString() : "";
            
            bool isTroy = (upsType != null && upsType.Contains("Troy", StringComparison.OrdinalIgnoreCase)) || 
                          (devName != null && devName.Contains("Troy", StringComparison.OrdinalIgnoreCase));
            
            if (!isTroy && catName.StartsWith("UPS"))
            {
                if (battPercent.HasValue && battPercent.Value > 0.0) {
                    if (battPercent.Value <= 25.0) {
                        metricStatus = "critical";
                        cat["has_battery_issue"] = true;
                    } else if (battPercent.Value <= 40.0) {
                        if (metricStatus != "critical") metricStatus = "warning";
                        cat["has_battery_issue"] = true;
                    }
                }
            }

            var upsPhaseType = row.ContainsKey("ups_phase_type") && row["ups_phase_type"] != null ? row["ups_phase_type"].ToString() : "";
            bool is3Phase = upsPhaseType != null && upsPhaseType.Contains("3");

            double vInA = row.ContainsKey("voltage_in_a") && row["voltage_in_a"] != null ? Convert.ToDouble(row["voltage_in_a"]) : 0;
            double vInB = row.ContainsKey("voltage_in_b") && row["voltage_in_b"] != null ? Convert.ToDouble(row["voltage_in_b"]) : 0;
            double vInC = row.ContainsKey("voltage_in_c") && row["voltage_in_c"] != null ? Convert.ToDouble(row["voltage_in_c"]) : 0;
            double vOutA = row.ContainsKey("voltage_out_a") && row["voltage_out_a"] != null ? Convert.ToDouble(row["voltage_out_a"]) : 0;
            double vOutB = row.ContainsKey("voltage_out_b") && row["voltage_out_b"] != null ? Convert.ToDouble(row["voltage_out_b"]) : 0;
            double vOutC = row.ContainsKey("voltage_out_c") && row["voltage_out_c"] != null ? Convert.ToDouble(row["voltage_out_c"]) : 0;

            if (catName.StartsWith("UPS"))
            {
                // Rule: If UPS has input voltage (>0) but output voltage is 0 -> CRITICAL
                if (vInA > 0 && vOutA == 0) {
                    metricStatus = "critical";
                    cat["has_voltage_issue"] = true;
                }

                double[] inVoltages = { vInA, vInB, vInC };
                foreach (var v in inVoltages) {
                    if (v > 0) {
                        if (v < 210.0 || v > 230.0) { metricStatus = "critical"; cat["has_voltage_issue"] = true; }
                        else if (v < 217.0 || v > 223.0) {
                            if (metricStatus != "critical") metricStatus = "warning";
                            cat["has_voltage_issue"] = true;
                        }
                    }
                }

                double[] outVoltages = { vOutA, vOutB, vOutC };
                foreach (var v in outVoltages) {
                    if (v > 0) {
                        if (v < 215.0 || v > 225.0) { metricStatus = "critical"; cat["has_voltage_issue"] = true; }
                        else if (v < 217.0 || v > 223.0) {
                            if (metricStatus != "critical") metricStatus = "warning";
                            cat["has_voltage_issue"] = true;
                        }
                    }
                }

                var freqIn = row.ContainsKey("input_frequency") && row["input_frequency"] != null ? Convert.ToDouble(row["input_frequency"]) : 0;
                if (freqIn > 0) {
                    if (freqIn < 45.0 || freqIn > 55.0) { metricStatus = "critical"; cat["has_freq_issue"] = true; }
                    else if (freqIn < 47.0 || freqIn > 53.0) {
                        if (metricStatus != "critical") metricStatus = "warning";
                        cat["has_freq_issue"] = true;
                    }
                }
                
                var loadPercent = row.ContainsKey("load_percent") && row["load_percent"] != null ? Convert.ToDouble(row["load_percent"]) : 0;
                if (loadPercent > 90.0) {
                    metricStatus = "critical";
                    cat["has_load_issue"] = true;
                } else if (loadPercent > 80.0) {
                    if (metricStatus != "critical") metricStatus = "warning";
                    cat["has_load_issue"] = true;
                }
            }

            row["metric_status"] = metricStatus;

            // Propagate status upwards to category (only if device is not actively acknowledged)
            string catStatus = cat["status"].ToString()!;
            if (!isAckActive)
            {
                if (metricStatus == "critical" && catStatus != "critical")
                {
                    cat["status"] = "critical";
                }
                else if (metricStatus == "warning" && catStatus == "normal")
                {
                    cat["status"] = "warning";
                }
            }
            
            devices.Add(rowObj);
        }

        // Post-processing to identify completely empty categories as "no-data"
        // and ensure all parameters are present for every site card
        var defaultRequiredCategories = new[] {
            "UPS DC",
            "UPS USER",
            "SENSOR SUHU RUANGAN",
            "PAC RUANGAN DC",
            "CCTV AVAILBILITY"
        };

        foreach (var kvp in sitesDict)
        {
            var s = kvp.Value as IDictionary<string, object>;
            var cats = s!["categories"] as IDictionary<string, object>;

            foreach (var reqCat in defaultRequiredCategories)
            {
                if (!cats.ContainsKey(reqCat))
                {
                    dynamic emptyCat = new ExpandoObject();
                    emptyCat.status = "no-data";
                    emptyCat.has_dead_device = false;
                    emptyCat.has_ack_device = false;
                    emptyCat.devices = new List<dynamic>();
                    cats[reqCat] = emptyCat;
                }
            }

            foreach (var cKvp in cats)
            {
                var c = cKvp.Value as IDictionary<string, object>;
                var dList = c!["devices"] as List<dynamic>;
                if (dList == null || dList.Count == 0)
                {
                    c["status"] = "no-data";
                }
                else
                {
                    int ackCount = 0;
                    int deadCount = 0;
                    int warnCount = 0;
                    int normalCount = 0;
                    int totalCount = dList.Count;

                    foreach (var d in dList)
                    {
                        var dDict = d as IDictionary<string, object>;
                        bool isAck = dDict != null && dDict.ContainsKey("is_acknowledged") && (bool)dDict["is_acknowledged"];
                        bool isOff = dDict != null && dDict.ContainsKey("is_offline") && (bool)dDict["is_offline"];
                        string mStat = dDict != null && dDict.ContainsKey("metric_status") ? dDict["metric_status"]?.ToString()?.ToLower() ?? "normal" : "normal";

                        if (isAck)
                        {
                            ackCount++;
                        }
                        else
                        {
                            if (isOff || mStat == "critical") deadCount++;
                            else if (mStat == "warning") warnCount++;
                            else normalCount++;
                        }
                    }

                    // Category status is strictly evaluated on unacknowledged devices!
                    if (deadCount > 0) c["status"] = "critical";
                    else if (warnCount > 0) c["status"] = "warning";
                    else c["status"] = "normal";

                    c["has_dead_device"] = deadCount > 0;
                    c["has_ack_device"] = ackCount > 0;
                    c["ack_count"] = ackCount;
                    c["total_count"] = totalCount;
                }
            }

            // Determine final site status based ONLY on PAC, SENSOR/SUHU, CCTV, and UPS DC (excluding UPS USER)
            bool hasCritical = false;
            bool hasWarning = false;
            bool hasNormal = false;

            foreach (var cKvp in cats)
            {
                var catKey = cKvp.Key.Trim().ToUpper();
                // Exclude UPS USER from determining site status
                if (catKey.Contains("USER"))
                {
                    continue;
                }

                // Check qualifying categories: PAC, SENSOR/SUHU, CCTV, UPS DC
                if (catKey.Contains("PAC") || catKey.Contains("SENSOR") || catKey.Contains("CCTV") || catKey == "UPS DC" || (catKey.StartsWith("UPS") && !catKey.Contains("USER")))
                {
                    var c = cKvp.Value as IDictionary<string, object>;
                    var cStatus = c?["status"]?.ToString()?.ToLower();
                    if (cStatus == "critical") hasCritical = true;
                    else if (cStatus == "warning") hasWarning = true;
                    else if (cStatus == "normal" || cStatus == "acknowledged") hasNormal = true;
                }
            }

            string finalSiteStatus = "no-data";
            if (hasCritical) finalSiteStatus = "critical";
            else if (hasWarning) finalSiteStatus = "warning";
            else if (hasNormal) finalSiteStatus = "normal";

            s["status"] = finalSiteStatus;

            // Calculate DCPI scores for each category and overall site
            var upsScores = new List<double>();
            var cctvScores = new List<double>();
            var suhuScores = new List<double>();

            // Preliminary pass to calculate aggregate metrics (UPS Readiness and PAC Site PA)
            double totalUpsLoad = 0.0;
            int numUps = 0;
            double sumPaAc = 0.0;
            int countAc = 0;
            double sumPaSensor = 0.0;
            int countSensor = 0;

            foreach (var cKvp in cats)
            {
                var catKey = cKvp.Key.Trim().ToUpper();
                var c = cKvp.Value as IDictionary<string, object>;
                var dList = c?["devices"] as List<dynamic>;
                if (dList == null) continue;

                if (catKey.StartsWith("UPS") && !catKey.Contains("USER"))
                {
                    foreach (var dObj in dList)
                    {
                        var d = dObj as IDictionary<string, object>;
                        if (d == null) continue;
                        numUps++;
                        if (d.ContainsKey("load") && d["load"] != null)
                            totalUpsLoad += Convert.ToDouble(d["load"]);
                    }
                }
                else if (catKey.Contains("PAC"))
                {
                    foreach (var dObj in dList)
                    {
                        var d = dObj as IDictionary<string, object>;
                        if (d == null) continue;
                        bool isOff = d.ContainsKey("is_offline") && Convert.ToBoolean(d["is_offline"]);
                        sumPaAc += isOff ? 0.0 : 100.0;
                        countAc++;
                    }
                }
                else if (catKey.Contains("SENSOR"))
                {
                    foreach (var dObj in dList)
                    {
                        var d = dObj as IDictionary<string, object>;
                        if (d == null) continue;
                        bool isOff = d.ContainsKey("is_offline") && Convert.ToBoolean(d["is_offline"]);
                        sumPaSensor += isOff ? 0.0 : 100.0;
                        countSensor++;
                    }
                }
            }

            double avgPaAc = countAc > 0 ? (sumPaAc / countAc) : 0.0;
            double avgPaSensor = countSensor > 0 ? (sumPaSensor / countSensor) : 0.0;
            double sitePacPa = 0.0;
            if (countAc > 0 && countSensor > 0) sitePacPa = (avgPaAc * 0.7) + (avgPaSensor * 0.3);
            else if (countAc > 0) sitePacPa = avgPaAc;
            else if (countSensor > 0) sitePacPa = avgPaSensor;

            foreach (var cKvp in cats)
            {
                var catKey = cKvp.Key.Trim().ToUpper();
                var c = cKvp.Value as IDictionary<string, object>;
                var dList = c?["devices"] as List<dynamic>;
                if (dList == null) continue;

                foreach (var dObj in dList)
                {
                    var d = dObj as IDictionary<string, object>;
                    if (d == null) continue;

                    bool isOffline = d.ContainsKey("is_offline") && Convert.ToBoolean(d["is_offline"]);
                    double avail = isOffline ? 0.0 : 100.0;

                    if (catKey.StartsWith("UPS"))
                    {
                        // Exclude UPS USER from DCPI calculations (User devices, not DC)
                        if (catKey.Contains("USER")) continue;

                        var vInList = new List<double>();
                        if (d.ContainsKey("voltage_in_a") && d["voltage_in_a"] != null && Convert.ToDouble(d["voltage_in_a"]) > 0) vInList.Add(Convert.ToDouble(d["voltage_in_a"]));
                        if (d.ContainsKey("voltage_in_b") && d["voltage_in_b"] != null && Convert.ToDouble(d["voltage_in_b"]) > 0) vInList.Add(Convert.ToDouble(d["voltage_in_b"]));
                        if (d.ContainsKey("voltage_in_c") && d["voltage_in_c"] != null && Convert.ToDouble(d["voltage_in_c"]) > 0) vInList.Add(Convert.ToDouble(d["voltage_in_c"]));
                        double vIn = vInList.Any() ? vInList.Average() : 220.0;

                        var vOutList = new List<double>();
                        if (d.ContainsKey("voltage_out_a") && d["voltage_out_a"] != null && Convert.ToDouble(d["voltage_out_a"]) > 0) vOutList.Add(Convert.ToDouble(d["voltage_out_a"]));
                        if (d.ContainsKey("voltage_out_b") && d["voltage_out_b"] != null && Convert.ToDouble(d["voltage_out_b"]) > 0) vOutList.Add(Convert.ToDouble(d["voltage_out_b"]));
                        if (d.ContainsKey("voltage_out_c") && d["voltage_out_c"] != null && Convert.ToDouble(d["voltage_out_c"]) > 0) vOutList.Add(Convert.ToDouble(d["voltage_out_c"]));
                        double vOut = vOutList.Any() ? vOutList.Average() : 220.0;

                        double fIn = d.ContainsKey("input_frequency") && d["input_frequency"] != null ? Convert.ToDouble(d["input_frequency"]) : 50.0;
                        double fOut = d.ContainsKey("output_frequency") && d["output_frequency"] != null ? Convert.ToDouble(d["output_frequency"]) : 50.0;
                        
                        double util = d.ContainsKey("load") && d["load"] != null ? Convert.ToDouble(d["load"]) : 0.0;
                        double runTime = d.ContainsKey("batt_run_time_remaining") && d["batt_run_time_remaining"] != null ? Convert.ToDouble(d["batt_run_time_remaining"]) : 0.0;

                        if (vIn <= 0) vIn = 220.0;
                        if (vOut <= 0) vOut = 220.0;
                        if (fIn <= 0) fIn = 50.0;
                        if (fOut <= 0) fOut = 50.0;

                        double loadScore = DcpiCalculator.GetScoreLoad(util, totalUpsLoad, numUps);
                        double runtimeScore = DcpiCalculator.GetScoreRuntime(runTime);

                        double devDcpi = DcpiCalculator.CalculateUpsDcpi(avail, fIn, fOut, vIn, vOut, loadScore, runtimeScore);
                        upsScores.Add(devDcpi);
                    }
                    else if (catKey.Contains("CCTV"))
                    {
                        double devDcpi = DcpiCalculator.CalculateCctvDcpi(avail);
                        cctvScores.Add(devDcpi);
                    }
                    else if (catKey.Contains("SENSOR") || catKey.Contains("PAC"))
                    {
                        double temp = 20.0;
                        double humid = 50.0;
                        if (d.ContainsKey("sl_temp") && d["sl_temp"] != null) temp = Convert.ToDouble(d["sl_temp"]);
                        else if (d.ContainsKey("pl_temp") && d["pl_temp"] != null) temp = Convert.ToDouble(d["pl_temp"]);

                        if (d.ContainsKey("sl_humid") && d["sl_humid"] != null) humid = Convert.ToDouble(d["sl_humid"]);
                        else if (d.ContainsKey("pl_humid") && d["pl_humid"] != null) humid = Convert.ToDouble(d["pl_humid"]);

                        double devDcpi = DcpiCalculator.CalculatePacDcpi(sitePacPa, humid, temp);
                        suhuScores.Add(devDcpi);
                    }
                }
            }

            double? dcpiUps = upsScores.Any() ? Math.Round(upsScores.Average(), 2) : (double?)null;
            double? dcpiCctv = cctvScores.Any() ? Math.Round(cctvScores.Average(), 2) : (double?)null;
            double? dcpiSuhu = suhuScores.Any() ? Math.Round(suhuScores.Average(), 2) : (double?)null;

            var siteCatScores = new List<double>();
            if (dcpiUps.HasValue) siteCatScores.Add(dcpiUps.Value);
            if (dcpiCctv.HasValue) siteCatScores.Add(dcpiCctv.Value);
            if (dcpiSuhu.HasValue) siteCatScores.Add(dcpiSuhu.Value);

            double? dcpiOverall = siteCatScores.Any() ? Math.Round(siteCatScores.Average(), 2) : (double?)null;

            s["dcpi_ups"] = dcpiUps;
            s["dcpi_cctv"] = dcpiCctv;
            s["dcpi_suhu"] = dcpiSuhu;
            s["dcpi_overall"] = dcpiOverall;
        }

        var resultList = new List<dynamic>();
        foreach (var kvp in sitesDict)
        {
            resultList.Add(kvp.Value);
        }

        return resultList;
    }
}





