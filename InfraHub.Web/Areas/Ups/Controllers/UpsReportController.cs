using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using InfraHub.Core.Interfaces;
using InfraHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InfraHub.Web.Areas.Ups.Controllers;

[Area("Ups")]
[Authorize]
[Route("[area]/Log")]
public class UpsReportController : Controller
{
    private readonly AppDbContext _context;
    private readonly IStoredProcedureExecutor _spExecutor;

    public UpsReportController(AppDbContext context, IStoredProcedureExecutor spExecutor)
    {
        _context = context;
        _spExecutor = spExecutor;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? site_id, [FromQuery] string? date_from, [FromQuery] string? date_to, [FromQuery] string resolution = "daily", [FromQuery] string aggregation = "average", [FromQuery] string ups_category = "ALL", [FromQuery] string time_from = "00:00", [FromQuery] string time_to = "23:59")
    {
        var roleName = User.FindFirst("RoleName")?.Value ?? "";
        ViewBag.IsAdmin = roleName == "Administrator";

        var sites = await _context.Sites.OrderBy(s => s.SiteCode).ToListAsync();
        ViewBag.Districts = sites;
        
        ViewBag.SiteId = site_id;
        ViewBag.DateFrom = date_from ?? DateTime.Now.ToString("yyyy-MM-dd");
        ViewBag.DateTo = date_to ?? DateTime.Now.ToString("yyyy-MM-dd");
        ViewBag.Resolution = resolution;
        ViewBag.Aggregation = aggregation;
        ViewBag.UpsCategory = ups_category;
        ViewBag.TimeFrom = time_from;
        ViewBag.TimeTo = time_to;
        ViewBag.ReportType = "Ups";

        var queryTimeTo = time_to;

        if (resolution == "daily" && !string.IsNullOrEmpty(site_id) && site_id != "ALL" && time_to == "23:59" && ViewBag.DateTo == DateTime.Now.ToString("yyyy-MM-dd"))
        {
            if (Guid.TryParse(site_id, out Guid siteGuid))
            {
                var todayStart = DateTime.Today;
                var todayEnd = DateTime.Today.AddDays(1);
                var maxTime = await _context.UpsLogs
                    .Include(l => l.Device)
                    .Where(l => l.Device.SiteId == siteGuid && l.DeviceTime >= todayStart && l.DeviceTime < todayEnd)
                    .MaxAsync(l => (DateTime?)l.DeviceTime);
                
                if (maxTime.HasValue)
                {
                    queryTimeTo = maxTime.Value.ToString("HH:mm");
                    ViewBag.TimeTo = queryTimeTo;
                }
            }
        }

        ViewBag.Report = null;
        ViewBag.ErrorMessage = null;

        if (!string.IsNullOrEmpty(site_id))
        {
            try {
                var reportData = await BuildReport(site_id, ViewBag.DateFrom, ViewBag.DateTo, resolution, aggregation, ups_category, time_from, queryTimeTo);
                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                };
                ViewBag.Report = System.Text.Json.JsonSerializer.Serialize(reportData, jsonOptions);
            } catch (Exception ex) { 
                Console.WriteLine($"[UPS Report] ERROR: {ex.Message}\n{ex.StackTrace}");
                ViewBag.ErrorMessage = ex.Message;
            }
        }

        return View();
    }

    [AllowAnonymous]
    [HttpGet("/api/ups/report/summary")]
    public async Task<IActionResult> GetSummary([FromQuery] string? site_id, [FromQuery] string? date_from, [FromQuery] string? date_to, [FromQuery] string resolution = "daily", [FromQuery] string aggregation = "average", [FromQuery] string ups_category = "ALL", [FromQuery] string time_from = "00:00", [FromQuery] string time_to = "23:59")
    {
        if (string.IsNullOrEmpty(site_id))
        {
            return BadRequest(new { status = "error", message = "Pilih site terlebih dahulu." });
        }

        try
        {
            var dateFrom = date_from ?? DateTime.Now.ToString("yyyy-MM-dd");
            var dateTo = date_to ?? DateTime.Now.ToString("yyyy-MM-dd");
            var queryTimeTo = time_to;

            if (resolution == "daily" && site_id != "ALL" && time_to == "23:59" && dateTo == DateTime.Now.ToString("yyyy-MM-dd"))
            {
                if (Guid.TryParse(site_id, out Guid siteGuid))
                {
                    var todayStart = DateTime.Today;
                    var todayEnd = DateTime.Today.AddDays(1);
                    var maxTime = await _context.UpsLogs
                        .Include(l => l.Device)
                        .Where(l => l.Device.SiteId == siteGuid && l.DeviceTime >= todayStart && l.DeviceTime < todayEnd)
                        .MaxAsync(l => (DateTime?)l.DeviceTime);
                    
                    if (maxTime.HasValue)
                    {
                        queryTimeTo = maxTime.Value.ToString("HH:mm");
                    }
                }
            }

            var reportData = await BuildReport(site_id, dateFrom, dateTo, resolution, aggregation, ups_category, time_from, queryTimeTo);
            return Json(new {
                status = "success",
                data = reportData,
                meta = new { siteId = site_id, dateFrom, dateTo, resolution, aggregation, upsCategory = ups_category, timeFrom = time_from, timeTo = queryTimeTo }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpGet("Sensor")]
    public async Task<IActionResult> SensorLog([FromQuery] string? site_id, [FromQuery] string? date_from, [FromQuery] string? date_to, [FromQuery] string resolution = "daily", [FromQuery] string? ups_category = "ALL")
    {
        ViewBag.IsAdmin = (User.FindFirst("RoleName")?.Value ?? "") == "Administrator";
        ViewBag.Districts = await _context.Sites.OrderBy(s => s.SiteCode).ToListAsync();
        ViewBag.SiteId = site_id;
        ViewBag.DateFrom = date_from ?? DateTime.Now.ToString("yyyy-MM-dd");
        ViewBag.DateTo = date_to ?? DateTime.Now.ToString("yyyy-MM-dd");
        ViewBag.Resolution = resolution;
        ViewBag.UpsCategory = ups_category ?? "ALL";
        ViewBag.ReportType = "Sensor";
        ViewBag.Report = null;
        ViewBag.ErrorMessage = null;

        if (!string.IsNullOrEmpty(site_id))
        {
            try
            {
                var reportData = await BuildSensorReportAsync(site_id, ViewBag.DateFrom, ViewBag.DateTo, resolution);
                ViewBag.Report = System.Text.Json.JsonSerializer.Serialize(reportData, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sensor Report] ERROR: {ex.Message}\n{ex.StackTrace}");
                ViewBag.ErrorMessage = ex.Message;
            }
        }

        return View("~/Areas/Ups/Views/UpsReport/SensorLog.cshtml");
    }

    [AllowAnonymous]
    [HttpGet("/api/ups/report/sensor")]
    public async Task<IActionResult> GetSensorSummary([FromQuery] string? site_id, [FromQuery] string? date_from, [FromQuery] string? date_to, [FromQuery] string resolution = "daily")
    {
        if (string.IsNullOrEmpty(site_id)) return BadRequest(new { status = "error", message = "Pilih site terlebih dahulu." });
        try
        {
            var dateFrom = date_from ?? DateTime.Now.ToString("yyyy-MM-dd");
            var dateTo = date_to ?? DateTime.Now.ToString("yyyy-MM-dd");
            var reportData = await BuildSensorReportAsync(site_id, dateFrom, dateTo, resolution);
            return Json(new { status = "success", data = reportData, meta = new { siteId = site_id, dateFrom, dateTo, resolution } });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpGet("Pac")]
    public async Task<IActionResult> PacLog([FromQuery] string? site_id, [FromQuery] string? date_from, [FromQuery] string? date_to, [FromQuery] string resolution = "daily", [FromQuery] string? ups_category = "ALL")
    {
        ViewBag.IsAdmin = (User.FindFirst("RoleName")?.Value ?? "") == "Administrator";
        ViewBag.Districts = await _context.Sites.OrderBy(s => s.SiteCode).ToListAsync();
        ViewBag.SiteId = site_id;
        ViewBag.DateFrom = date_from ?? DateTime.Now.ToString("yyyy-MM-dd");
        ViewBag.DateTo = date_to ?? DateTime.Now.ToString("yyyy-MM-dd");
        ViewBag.Resolution = resolution;
        ViewBag.UpsCategory = ups_category ?? "ALL";
        ViewBag.ReportType = "Pac";
        ViewBag.Report = null;
        ViewBag.ErrorMessage = null;

        if (!string.IsNullOrEmpty(site_id))
        {
            try
            {
                var reportData = await BuildPacReportAsync(site_id, ViewBag.DateFrom, ViewBag.DateTo, resolution);
                ViewBag.Report = System.Text.Json.JsonSerializer.Serialize(reportData, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PAC Report] ERROR: {ex.Message}\n{ex.StackTrace}");
                ViewBag.ErrorMessage = ex.Message;
            }
        }

        return View("~/Areas/Ups/Views/UpsReport/PacLog.cshtml");
    }

    [AllowAnonymous]
    [HttpGet("/api/ups/report/pac")]
    public async Task<IActionResult> GetPacSummary([FromQuery] string? site_id, [FromQuery] string? date_from, [FromQuery] string? date_to, [FromQuery] string resolution = "daily")
    {
        if (string.IsNullOrEmpty(site_id)) return BadRequest(new { status = "error", message = "Pilih site terlebih dahulu." });
        try
        {
            var dateFrom = date_from ?? DateTime.Now.ToString("yyyy-MM-dd");
            var dateTo = date_to ?? DateTime.Now.ToString("yyyy-MM-dd");
            var reportData = await BuildPacReportAsync(site_id, dateFrom, dateTo, resolution);
            return Json(new { status = "success", data = reportData, meta = new { siteId = site_id, dateFrom, dateTo, resolution } });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpGet("Cctv")]
    public async Task<IActionResult> CctvLog([FromQuery] string? site_id, [FromQuery] string? date_from, [FromQuery] string? date_to, [FromQuery] string resolution = "daily")
    {
        ViewBag.IsAdmin = (User.FindFirst("RoleName")?.Value ?? "") == "Administrator";
        ViewBag.Districts = await _context.Sites.OrderBy(s => s.SiteCode).ToListAsync();
        ViewBag.SiteId = site_id;
        ViewBag.DateFrom = date_from ?? DateTime.Now.ToString("yyyy-MM-dd");
        ViewBag.DateTo = date_to ?? DateTime.Now.ToString("yyyy-MM-dd");
        ViewBag.Resolution = resolution;
        ViewBag.ReportType = "Cctv";
        ViewBag.Report = null;
        ViewBag.ErrorMessage = null;

        if (!string.IsNullOrEmpty(site_id))
        {
            try
            {
                var reportData = await BuildCctvReportAsync(site_id, ViewBag.DateFrom, ViewBag.DateTo, resolution);
                ViewBag.Report = System.Text.Json.JsonSerializer.Serialize(reportData, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CCTV Report] ERROR: {ex.Message}\n{ex.StackTrace}");
                ViewBag.ErrorMessage = ex.Message;
            }
        }

        return View("~/Areas/Ups/Views/UpsReport/CctvLog.cshtml");
    }

    [AllowAnonymous]
    [HttpGet("/api/ups/report/cctv")]
    public async Task<IActionResult> GetCctvSummary([FromQuery] string? site_id, [FromQuery] string? date_from, [FromQuery] string? date_to, [FromQuery] string resolution = "daily")
    {
        if (string.IsNullOrEmpty(site_id)) return BadRequest(new { status = "error", message = "Pilih site terlebih dahulu." });
        try
        {
            var dateFrom = date_from ?? DateTime.Now.ToString("yyyy-MM-dd");
            var dateTo = date_to ?? DateTime.Now.ToString("yyyy-MM-dd");
            var reportData = await BuildCctvReportAsync(site_id, dateFrom, dateTo, resolution);
            return Json(new { status = "success", data = reportData, meta = new { siteId = site_id, dateFrom, dateTo, resolution } });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    private async Task<dynamic> BuildReport(string siteId, string dateFrom, string dateTo, string resolution, string aggregation, string upsCategory, string timeFrom, string timeTo)
    {
        bool isAllSites = siteId == "ALL";
        Guid siteGuid = Guid.Empty;
        if (!isAllSites && !Guid.TryParse(siteId, out siteGuid))
        {
            throw new Exception("Invalid site ID format.");
        }

        DateTime fromDt, toDt;
        string periodLabel;
        if (resolution == "daily")
        {
            fromDt = DateTime.Parse($"{dateTo} {timeFrom}");
            toDt = DateTime.Parse($"{dateTo} {timeTo}").AddSeconds(59);
            periodLabel = $"{fromDt:dd MMM yyyy}, {timeFrom} - {timeTo}";
        }
        else
        {
            fromDt = DateTime.Parse(dateFrom).Date;
            toDt = DateTime.Parse(dateTo).Date.AddDays(1).AddSeconds(-1);
            periodLabel = $"{fromDt:dd MMM} – {toDt:dd MMM yyyy}";
        }

        if (upsCategory == "KOMPARASI") {
            return await BuildKomparasiReport(siteGuid, isAllSites, fromDt, toDt, periodLabel, resolution, aggregation);
        }

        var sqlDevices = @"
            SELECT 
                d.id AS device_id, 
                d.device_name, 
                COALESCE(d.ups_phase_type, '3/3') AS ups_phase_type, 
                COALESCE(d.ups_category, 'UPS DC') AS ups_category
            FROM tbl_m_devices d
            INNER JOIN tbl_r_asset_types at ON at.id = d.asset_type_id
            LEFT JOIN tbl_m_zones z ON z.id = d.zone_id
            WHERE d.is_active = 1
              AND UPPER(at.type_name) = 'UPS'";

        if (!isAllSites) {
            sqlDevices += " AND COALESCE(z.site_id, d.site_id) = @p0";
        }
        if (upsCategory == "UPS DC" || upsCategory == "UPS USER") {
            sqlDevices += $" AND COALESCE(d.ups_category, 'UPS DC') = '{upsCategory}'";
        }
        sqlDevices += " ORDER BY d.device_name";

        var devices = isAllSites ? await _spExecutor.ExecuteSqlAsync(sqlDevices) : await _spExecutor.ExecuteSqlAsync(sqlDevices, new object[] { siteGuid });

        var deviceIds = new List<string>();
        var deviceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var devicePhaseTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (isAllSites) {
            deviceIds.Add("ALL");
            deviceNames["ALL"] = "All Sites (Global Average)";
            devicePhaseTypes["ALL"] = "3/3";
        } else {
            foreach (var item in devices)
            {
                var d = (IDictionary<string, object>)item;
                var did = d["device_id"]?.ToString() ?? "";
                deviceIds.Add(did);
                deviceNames[did] = d["device_name"]?.ToString() ?? did;
                devicePhaseTypes[did] = d["ups_phase_type"]?.ToString() ?? "3/3";
            }
        }

        if (!deviceIds.Any()) return new { meta = new { total_devices = 0, period_label = periodLabel, device_names = deviceNames, dcpi_site_score = 0.0, dcpi_devices = new Dictionary<string, double>() }, stats = new {}, trends = new {}, comparison = new { labels = new string[0], input = new string[0], output = new string[0] } };

        // Generate Time Keys
        var keys = new List<string>();
        var labels = new List<string>();
        
        if (resolution == "weekly" || resolution == "monthly")
        {
            var cursor = fromDt.Date;
            var end = toDt.Date;
            while (cursor <= end)
            {
                keys.Add(cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                labels.Add(cursor.ToString("dd MMM", CultureInfo.InvariantCulture));
                cursor = cursor.AddDays(1);
            }
        }
        else // Daily
        {
            if (aggregation == "actual")
            {
                var cursor = fromDt;
                var roundedStart = fromDt.AddMinutes(-(fromDt.Minute % 3)).AddSeconds(-fromDt.Second);
                var cur = roundedStart;
                while (cur <= toDt)
                {
                    keys.Add(cur.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                    labels.Add(cur.ToString("HH:mm", CultureInfo.InvariantCulture));
                    cur = cur.AddMinutes(3);
                }
            }
            else
            {
                var cursor = new DateTime(fromDt.Year, fromDt.Month, fromDt.Day, fromDt.Hour, 0, 0);
                while (cursor <= toDt)
                {
                    keys.Add(cursor.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture));
                    labels.Add(cursor.ToString("HH:00", CultureInfo.InvariantCulture));
                    cursor = cursor.AddHours(1);
                }
            }
        }

        // Summary Stats
        var sqlStats = @"
            SELECT 
                AVG((l.voltage_in_a + 
                     CASE WHEN COALESCE(d.ups_phase_type, '3/3') IN ('3/3', '3/1') AND l.voltage_in_b > 0 THEN l.voltage_in_b ELSE 0 END + 
                     CASE WHEN COALESCE(d.ups_phase_type, '3/3') IN ('3/3', '3/1') AND l.voltage_in_c > 0 THEN l.voltage_in_c ELSE 0 END)
                    / (1.0 + 
                       CASE WHEN COALESCE(d.ups_phase_type, '3/3') IN ('3/3', '3/1') AND l.voltage_in_b > 0 THEN 1.0 ELSE 0 END + 
                       CASE WHEN COALESCE(d.ups_phase_type, '3/3') IN ('3/3', '3/1') AND l.voltage_in_c > 0 THEN 1.0 ELSE 0 END)
                ) AS avg_vin,
                AVG(l.voltage_in_a)          AS avg_vin_a,
                AVG(CASE WHEN COALESCE(d.ups_phase_type, '3/3') IN ('3/3', '3/1') THEN l.voltage_in_b END) AS avg_vin_b,
                AVG(CASE WHEN COALESCE(d.ups_phase_type, '3/3') IN ('3/3', '3/1') THEN l.voltage_in_c END) AS avg_vin_c,
                AVG((l.voltage_out_a + 
                     CASE WHEN COALESCE(d.ups_phase_type, '3/3') = '3/3' AND l.voltage_out_b > 0 THEN l.voltage_out_b ELSE 0 END + 
                     CASE WHEN COALESCE(d.ups_phase_type, '3/3') = '3/3' AND l.voltage_out_c > 0 THEN l.voltage_out_c ELSE 0 END)
                    / (1.0 + 
                       CASE WHEN COALESCE(d.ups_phase_type, '3/3') = '3/3' AND l.voltage_out_b > 0 THEN 1.0 ELSE 0 END + 
                       CASE WHEN COALESCE(d.ups_phase_type, '3/3') = '3/3' AND l.voltage_out_c > 0 THEN 1.0 ELSE 0 END)
                ) AS avg_vout,
                AVG(l.voltage_out_a)         AS avg_vout_a,
                AVG(CASE WHEN COALESCE(d.ups_phase_type, '3/3') = '3/3' THEN l.voltage_out_b END) AS avg_vout_b,
                AVG(CASE WHEN COALESCE(d.ups_phase_type, '3/3') = '3/3' THEN l.voltage_out_c END) AS avg_vout_c,
                AVG(l.input_frequency)       AS avg_freq_in,
                AVG(l.output_frequency)      AS avg_freq_out,
                AVG(l.grounding_resistance)  AS avg_ground,
                MIN(l.grounding_resistance)  AS min_ground,
                MAX(l.grounding_resistance)  AS max_ground,
                AVG(l.battery_percent)       AS avg_bat_pct,
                MIN(l.battery_percent)       AS min_bat_pct,
                AVG(l.battery_voltage)       AS avg_bat_v,
                AVG(l.ambient_temp)          AS avg_temp,
                MIN(l.ambient_temp)          AS min_temp,
                MAX(l.ambient_temp)          AS max_temp,
                AVG(l.room_temp)             AS avg_room_temp,
                MIN(l.room_temp)             AS min_room_temp,
                MAX(l.room_temp)             AS max_room_temp,
                AVG(l.load_percent)          AS avg_load,
                AVG(l.total_watts)           AS avg_watts
            FROM tbl_t_ups_logs l
            INNER JOIN tbl_m_devices d ON d.id = l.device_id
            LEFT JOIN tbl_m_zones z ON z.id = d.zone_id
            INNER JOIN tbl_r_asset_types at ON at.id = d.asset_type_id
            WHERE d.is_active = 1
              AND UPPER(at.type_name) = 'UPS'
              AND l.device_time BETWEEN @p0 AND @p1";

        if (!isAllSites) {
            sqlStats += " AND COALESCE(z.site_id, d.site_id) = @p2";
        }
        if (upsCategory == "UPS DC" || upsCategory == "UPS USER") {
            sqlStats += $" AND COALESCE(d.ups_category, 'UPS DC') = '{upsCategory}'";
        }

        var statsRaw = isAllSites 
            ? await _spExecutor.ExecuteSqlAsync(sqlStats, new object[] { fromDt, toDt })
            : await _spExecutor.ExecuteSqlAsync(sqlStats, new object[] { fromDt, toDt, siteGuid });
            
        var statsDict = statsRaw.FirstOrDefault() as IDictionary<string, object>;
        
        var stats = new Dictionary<string, object>();
        if (statsDict != null) {
            foreach(var kv in statsDict) {
                stats[kv.Key] = kv.Value != null && kv.Value != DBNull.Value ? Math.Round(Convert.ToDouble(kv.Value), 1) : (object)null!;
            }
        }

        string sqlTrends = "";
        var trendParams = new List<object>();
        
        if (isAllSites) {
            sqlTrends = @"
                SELECT 
                    'ALL' as device_id, 
                    CAST(l.device_time AS DATE) as device_time,
                    AVG(l.voltage_in_a) as voltage_in_a, 
                    AVG(CASE WHEN COALESCE(d.ups_phase_type, '3/3') IN ('3/3', '3/1') THEN NULLIF(l.voltage_in_b, 0) END) as voltage_in_b, 
                    AVG(CASE WHEN COALESCE(d.ups_phase_type, '3/3') IN ('3/3', '3/1') THEN NULLIF(l.voltage_in_c, 0) END) as voltage_in_c, 
                    AVG(l.current_in_a) as current_in_a, 
                    AVG(l.input_frequency) as input_frequency,
                    AVG(l.voltage_out_a) as voltage_out_a, 
                    AVG(CASE WHEN COALESCE(d.ups_phase_type, '3/3') = '3/3' THEN NULLIF(l.voltage_out_b, 0) END) as voltage_out_b, 
                    AVG(CASE WHEN COALESCE(d.ups_phase_type, '3/3') = '3/3' THEN NULLIF(l.voltage_out_c, 0) END) as voltage_out_c, 
                    AVG(l.current_out_a) as current_out_a, 
                    AVG(l.output_frequency) as output_frequency,
                    AVG(l.grounding_resistance) as grounding_resistance, 
                    AVG(l.battery_voltage) as battery_voltage, 
                    AVG(l.battery_percent) as battery_percent, 
                    AVG(l.battery_runtime_min) as battery_runtime_min,
                    AVG(l.ambient_temp) as ambient_temp, 
                    AVG(l.room_temp) as room_temp, 
                    AVG(l.load_percent) as load_percent, 
                    AVG(l.total_watts) as total_watts
                FROM tbl_t_ups_logs l
                INNER JOIN tbl_m_devices d ON d.id = l.device_id
                INNER JOIN tbl_r_asset_types at ON at.id = d.asset_type_id
                WHERE d.is_active = 1
                  AND UPPER(at.type_name) = 'UPS'
                  AND l.device_time BETWEEN @p0 AND @p1";

            if (upsCategory == "UPS DC" || upsCategory == "UPS USER") {
                sqlTrends += $" AND COALESCE(d.ups_category, 'UPS DC') = '{upsCategory}'";
            }
            
            sqlTrends += " GROUP BY CAST(l.device_time AS DATE) ORDER BY CAST(l.device_time AS DATE)";
            trendParams.Add(fromDt);
            trendParams.Add(toDt);
        } else {
            var inParams = string.Join(",", deviceIds.Select((_, i) => $"@p{i}"));
            if (resolution == "daily") {
                sqlTrends = $@"
                    SELECT 
                        l.device_id, l.device_time, 
                        l.voltage_in_a, l.voltage_in_b, l.voltage_in_c, l.current_in_a, l.input_frequency,
                        l.voltage_out_a, l.voltage_out_b, l.voltage_out_c, l.current_out_a, l.output_frequency,
                        l.grounding_resistance, l.battery_voltage, l.battery_percent, l.battery_runtime_min,
                        l.ambient_temp, l.room_temp, l.load_percent, l.total_watts
                    FROM tbl_t_ups_logs l
                    WHERE l.device_id IN ({inParams})
                      AND l.device_time BETWEEN @p{deviceIds.Count} AND @p{deviceIds.Count + 1}
                    ORDER BY l.device_time";
            } else {
                sqlTrends = $@"
                    SELECT 
                        l.device_id, 
                        CAST(l.device_time AS DATE) as device_time, 
                        AVG(l.voltage_in_a) as voltage_in_a, 
                        AVG(l.voltage_in_b) as voltage_in_b, 
                        AVG(l.voltage_in_c) as voltage_in_c, 
                        AVG(l.current_in_a) as current_in_a, 
                        AVG(l.input_frequency) as input_frequency,
                        AVG(l.voltage_out_a) as voltage_out_a, 
                        AVG(l.voltage_out_b) as voltage_out_b, 
                        AVG(l.voltage_out_c) as voltage_out_c, 
                        AVG(l.current_out_a) as current_out_a, 
                        AVG(l.output_frequency) as output_frequency,
                        AVG(l.grounding_resistance) as grounding_resistance, 
                        AVG(l.battery_voltage) as battery_voltage, 
                        AVG(l.battery_percent) as battery_percent, 
                        AVG(l.battery_runtime_min) as battery_runtime_min,
                        AVG(l.ambient_temp) as ambient_temp, 
                        AVG(l.room_temp) as room_temp, 
                        AVG(l.load_percent) as load_percent, 
                        AVG(l.total_watts) as total_watts
                    FROM tbl_t_ups_logs l
                    WHERE l.device_id IN ({inParams})
                      AND l.device_time BETWEEN @p{deviceIds.Count} AND @p{deviceIds.Count + 1}
                    GROUP BY l.device_id, CAST(l.device_time AS DATE)
                    ORDER BY CAST(l.device_time AS DATE)";
            }
            foreach (var did in deviceIds) trendParams.Add(Guid.Parse(did));
            trendParams.Add(fromDt);
            trendParams.Add(toDt);
        }

        var trendsRaw = await _spExecutor.ExecuteSqlAsync(sqlTrends, trendParams.ToArray());

        var metrics = new[] { "voltage_in_a", "voltage_in_b", "voltage_in_c", "input_frequency", "voltage_out_a", "voltage_out_b", "voltage_out_c", "output_frequency", "grounding_resistance", "battery_voltage", "battery_percent", "ambient_temp", "room_temp", "load_percent" };
        
        var trendsPerMetric = new Dictionary<string, object>();

        var zeroOnDownMetrics = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "voltage_in_a", "voltage_in_b", "voltage_in_c",
            "voltage_out_a", "voltage_out_b", "voltage_out_c",
            "input_frequency", "output_frequency", "load_percent"
        };

        // Track raw timestamps per device for bucket-based availability calculation
        var deviceTimestamps = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);
        foreach (var did in deviceIds) deviceTimestamps[did] = new List<DateTime>();

        foreach (var metric in metrics) {
            var metricData = new Dictionary<string, Dictionary<string, List<double>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var did in deviceIds) { metricData[did] = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase); }

            foreach (var item in trendsRaw) {
                var row = (IDictionary<string, object>)item;
                var did = row["device_id"]?.ToString() ?? "";
                if (!metricData.ContainsKey(did)) continue;

                DateTime dt;
                if (row["device_time"] is DateTime dtVal) {
                    dt = dtVal;
                } else if (!DateTime.TryParse(row["device_time"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) {
                    continue;
                }

                if (metric == "voltage_in_a") {
                    deviceTimestamps[did].Add(dt);
                }

                string key;
                if (resolution == "daily") {
                    if (aggregation == "actual") {
                        var min = dt.Minute;
                        var rounded = dt.AddMinutes(-(min % 3)).AddSeconds(-dt.Second);
                        key = rounded.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                    } else {
                        key = dt.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture);
                    }
                } else {
                    key = dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                if (row[metric] != null && row[metric] != DBNull.Value) {
                    if (!metricData[did].ContainsKey(key)) metricData[did][key] = new List<double>();
                    metricData[did][key].Add(Convert.ToDouble(row[metric]));
                }
            }

            var isZeroMetric = zeroOnDownMetrics.Contains(metric);
            var devicesList = new List<object>();

            foreach (var did in deviceIds) {
                var values = new List<double?>();
                foreach(var key in keys) {
                    if (metricData[did].ContainsKey(key) && metricData[did][key].Count > 0) {
                        values.Add(Math.Round(metricData[did][key].Average(), 2));
                    } else {
                        values.Add(isZeroMetric ? 0.0 : (double?)null);
                    }
                }
                devicesList.Add(new Dictionary<string, object> { 
                    { "id", did }, 
                    { "name", deviceNames[did] }, 
                    { "values", values } 
                });
            }

            trendsPerMetric[metric] = new Dictionary<string, object> { 
                { "labels", labels }, 
                { "devices", devicesList } 
            };
        }

        // Calculate DCPI for Each UPS Device & Site
        var totalMinutes = (toDt - fromDt).TotalMinutes;
        var totalBuckets = (int)Math.Max(1, Math.Ceiling(totalMinutes / 5.0));

        var deviceDcpis = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double siteDcpiSum = 0;
        int siteDcpiCount = 0;

        Func<string, string, double> getAvgValForDevice = (mKey, devId) => {
            if (!trendsPerMetric.ContainsKey(mKey)) return 0;
            var mDict = (Dictionary<string, object>)trendsPerMetric[mKey];
            var mList = (List<object>)mDict["devices"];
            var mDev = mList.Cast<Dictionary<string, object>>().FirstOrDefault(d => string.Equals(d["id"]?.ToString(), devId, StringComparison.OrdinalIgnoreCase));
            if (mDev != null)
            {
                var mVals = (List<double?>)mDev["values"];
                var valid = mVals.Where(v => v.HasValue && v.Value > 0).ToList();
                if (valid.Any()) return valid.Average(v => v!.Value);
            }
            return 0;
        };

        // Preliminary pass to calculate aggregate metrics (UPS Readiness load)
        double totalUpsLoad = 0.0;
        int numUps = 0;
        foreach (var did in deviceIds)
        {
            if (did != "ALL")
            {
                numUps++;
                totalUpsLoad += getAvgValForDevice("load", did);
            }
        }

        // Pre-compute 1/1 voltage averages per device directly from raw log records
        var devVoltIn = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
        var devVoltOut = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);

        if (isAllSites)
        {
            double? avgVin = stats.ContainsKey("avg_vin") && stats["avg_vin"] != null ? (double?)Math.Round(Convert.ToDouble(stats["avg_vin"]), 1) : null;
            double? avgVout = stats.ContainsKey("avg_vout") && stats["avg_vout"] != null ? (double?)Math.Round(Convert.ToDouble(stats["avg_vout"]), 1) : null;
            devVoltIn["ALL"] = avgVin;
            devVoltOut["ALL"] = avgVout;
        }
        else
        {
            foreach (var did in deviceIds)
            {
                var pType = devicePhaseTypes.ContainsKey(did) ? devicePhaseTypes[did] : "3/3";
                bool is3pIn = pType == "3/3" || pType == "3/1";
                bool is3pOut = pType == "3/3";

                var inVals = new List<double>();
                var outVals = new List<double>();

                foreach (var item in trendsRaw)
                {
                    var r = (IDictionary<string, object>)item;
                    if (!r.ContainsKey("device_id") || !string.Equals(r["device_id"]?.ToString(), did, StringComparison.OrdinalIgnoreCase))
                        continue;

                    double va = r.ContainsKey("voltage_in_a") && r["voltage_in_a"] != null && r["voltage_in_a"] != DBNull.Value ? Convert.ToDouble(r["voltage_in_a"]) : 0;
                    double vb = is3pIn && r.ContainsKey("voltage_in_b") && r["voltage_in_b"] != null && r["voltage_in_b"] != DBNull.Value ? Convert.ToDouble(r["voltage_in_b"]) : 0;
                    double vc = is3pIn && r.ContainsKey("voltage_in_c") && r["voltage_in_c"] != null && r["voltage_in_c"] != DBNull.Value ? Convert.ToDouble(r["voltage_in_c"]) : 0;

                    int inCount = 0;
                    double inSum = 0;
                    if (va > 0) { inSum += va; inCount++; }
                    if (vb > 0) { inSum += vb; inCount++; }
                    if (vc > 0) { inSum += vc; inCount++; }
                    if (inCount > 0) inVals.Add(inSum / inCount);

                    double voa = r.ContainsKey("voltage_out_a") && r["voltage_out_a"] != null && r["voltage_out_a"] != DBNull.Value ? Convert.ToDouble(r["voltage_out_a"]) : 0;
                    double vob = is3pOut && r.ContainsKey("voltage_out_b") && r["voltage_out_b"] != null && r["voltage_out_b"] != DBNull.Value ? Convert.ToDouble(r["voltage_out_b"]) : 0;
                    double voc = is3pOut && r.ContainsKey("voltage_out_c") && r["voltage_out_c"] != null && r["voltage_out_c"] != DBNull.Value ? Convert.ToDouble(r["voltage_out_c"]) : 0;

                    int outCount = 0;
                    double outSum = 0;
                    if (voa > 0) { outSum += voa; outCount++; }
                    if (vob > 0) { outSum += vob; outCount++; }
                    if (voc > 0) { outSum += voc; outCount++; }
                    if (outCount > 0) outVals.Add(outSum / outCount);
                }

                if (inVals.Any())
                {
                    devVoltIn[did] = Math.Round(inVals.Average(), 1);
                }
                else
                {
                    var vInFallback = new List<double>();
                    double vInA = getAvgValForDevice("voltage_in_a", did);
                    double vInB = is3pIn ? getAvgValForDevice("voltage_in_b", did) : 0;
                    double vInC = is3pIn ? getAvgValForDevice("voltage_in_c", did) : 0;
                    if (vInA > 0) vInFallback.Add(vInA);
                    if (vInB > 0) vInFallback.Add(vInB);
                    if (vInC > 0) vInFallback.Add(vInC);
                    devVoltIn[did] = vInFallback.Any() ? Math.Round(vInFallback.Average(), 1) : (double?)null;
                }

                if (outVals.Any())
                {
                    devVoltOut[did] = Math.Round(outVals.Average(), 1);
                }
                else
                {
                    var vOutFallback = new List<double>();
                    double vOutA = getAvgValForDevice("voltage_out_a", did);
                    double vOutB = is3pOut ? getAvgValForDevice("voltage_out_b", did) : 0;
                    double vOutC = is3pOut ? getAvgValForDevice("voltage_out_c", did) : 0;
                    if (vOutA > 0) vOutFallback.Add(vOutA);
                    if (vOutB > 0) vOutFallback.Add(vOutB);
                    if (vOutC > 0) vOutFallback.Add(vOutC);
                    devVoltOut[did] = vOutFallback.Any() ? Math.Round(vOutFallback.Average(), 1) : (double?)null;
                }
            }

            if (deviceIds.Count == 1)
            {
                var singleDid = deviceIds[0];
                if (stats.ContainsKey("avg_vin") && stats["avg_vin"] != null) devVoltIn[singleDid] = Math.Round(Convert.ToDouble(stats["avg_vin"]), 1);
                if (stats.ContainsKey("avg_vout") && stats["avg_vout"] != null) devVoltOut[singleDid] = Math.Round(Convert.ToDouble(stats["avg_vout"]), 1);
            }
        }

        foreach (var did in deviceIds)
        {
            double availability = 0;
            if (deviceTimestamps.ContainsKey(did) && deviceTimestamps[did].Any())
            {
                var activeBuckets = new HashSet<int>();
                foreach (var dt in deviceTimestamps[did])
                {
                    if (dt >= fromDt && dt <= toDt)
                    {
                        var bIdx = (int)((dt - fromDt).TotalMinutes / 5.0);
                        if (bIdx >= 0 && bIdx < totalBuckets) activeBuckets.Add(bIdx);
                    }
                }
                availability = Math.Min(100.0, Math.Round(((double)activeBuckets.Count / totalBuckets) * 100.0, 2));
            }

            double freqIn = getAvgValForDevice("input_frequency", did);
            double freqOut = getAvgValForDevice("output_frequency", did);
            double voltIn = devVoltIn.ContainsKey(did) && devVoltIn[did].HasValue ? devVoltIn[did]!.Value : 220.0;
            double voltOut = devVoltOut.ContainsKey(did) && devVoltOut[did].HasValue ? devVoltOut[did]!.Value : 220.0;
            double util = getAvgValForDevice("load", did);
            double runTime = getAvgValForDevice("batt_run_time_remaining", did);

            double loadScore = DcpiCalculator.GetScoreLoad(util, totalUpsLoad, numUps);
            double runtimeScore = DcpiCalculator.GetScoreRuntime(runTime);

            double dcpi = DcpiCalculator.CalculateUpsDcpi(availability, freqIn, freqOut, voltIn, voltOut, loadScore, runtimeScore);
            deviceDcpis[did] = dcpi;

            if (did != "ALL")
            {
                siteDcpiSum += dcpi;
                siteDcpiCount++;
            }
        }

        // Comparison Data (Input vs Output Voltage) - 1/1 Average across active phases
        var compInputs = deviceIds.Select(did => devVoltIn.ContainsKey(did) ? devVoltIn[did] : null).ToList();
        var compOutputs = deviceIds.Select(did => devVoltOut.ContainsKey(did) ? devVoltOut[did] : null).ToList();

        return new {
            meta = new {
                generated_at = DateTime.UtcNow.ToString("O"),
                period_label = periodLabel,
                total_devices = isAllSites ? devices.Count() : deviceIds.Count,
                resolution,
                aggregation,
                device_phase_types = devicePhaseTypes,
                device_names = deviceNames,
                dcpi_site_score = isAllSites ? (deviceDcpis.ContainsKey("ALL") ? deviceDcpis["ALL"] : 0) : (siteDcpiCount > 0 ? Math.Round(siteDcpiSum / siteDcpiCount, 2) : 0),
                dcpi_devices = deviceDcpis
            },
            stats = stats,
            trends = trendsPerMetric,
            comparison = new {
                labels = deviceIds.Select(d => deviceNames[d]).ToList(),
                input = compInputs,
                output = compOutputs
            }
        };
    }

    private async Task<dynamic> BuildKomparasiReport(Guid siteGuid, bool isAllSites, DateTime fromDt, DateTime toDt, string periodLabel, string resolution, string aggregation)
    {
        var sqlTrends = @"
            SELECT 
                COALESCE(s.site_code, 'Unknown') as site_code,
                COALESCE(d.ups_category, 'UPS DC') as category,
                AVG(l.voltage_in_a) as voltage_in_a, 
                AVG(l.voltage_in_b) as voltage_in_b, 
                AVG(l.voltage_in_c) as voltage_in_c, 
                AVG(l.current_in_a) as current_in_a, 
                AVG(l.input_frequency) as input_frequency,
                AVG(l.voltage_out_a) as voltage_out_a, 
                AVG(l.voltage_out_b) as voltage_out_b, 
                AVG(l.voltage_out_c) as voltage_out_c, 
                AVG(l.current_out_a) as current_out_a, 
                AVG(l.output_frequency) as output_frequency,
                AVG(l.grounding_resistance) as grounding_resistance, 
                AVG(l.battery_voltage) as battery_voltage, 
                AVG(l.battery_percent) as battery_percent, 
                AVG(l.battery_runtime_min) as battery_runtime_min,
                AVG(l.ambient_temp) as ambient_temp, 
                AVG(l.room_temp) as room_temp, 
                AVG(l.load_percent) as load_percent, 
                AVG(l.total_watts) as total_watts
            FROM tbl_t_ups_logs l
            INNER JOIN tbl_m_devices d ON d.id = l.device_id
            INNER JOIN tbl_r_asset_types at ON at.id = d.asset_type_id
            LEFT JOIN tbl_m_zones z ON z.id = d.zone_id
            LEFT JOIN tbl_m_sites s ON s.id = z.site_id OR s.id = d.site_id
            WHERE d.is_active = 1
              AND UPPER(at.type_name) = 'UPS'
              AND l.device_time BETWEEN @p0 AND @p1";

        if (!isAllSites) {
            sqlTrends += " AND COALESCE(z.site_id, d.site_id) = @p2";
        }
        
        sqlTrends += @"
            GROUP BY COALESCE(s.site_code, 'Unknown'), COALESCE(d.ups_category, 'UPS DC')
            ORDER BY COALESCE(s.site_code, 'Unknown'), COALESCE(d.ups_category, 'UPS DC')";

        var trendParams = new List<object> { fromDt, toDt };
        if (!isAllSites) {
            trendParams.Add(siteGuid);
        }

        var trendRows = await _spExecutor.ExecuteSqlAsync(sqlTrends, trendParams.ToArray());

        var siteCodes = trendRows.Select(r => ((IDictionary<string, object>)r)["site_code"]?.ToString() ?? "Unknown").Distinct().ToList();
        
        var metrics = new string[] { 
            "voltage_in_a", "voltage_in_b", "voltage_in_c", "current_in_a", "input_frequency",
            "voltage_out_a", "voltage_out_b", "voltage_out_c", "current_out_a", "output_frequency",
            "grounding_resistance", "battery_voltage", "battery_percent", "battery_runtime_min",
            "ambient_temp", "room_temp", "load_percent", "total_watts"
        };
        
        var trendsPerMetric = new Dictionary<string, object>();
        
        foreach (var metric in metrics) {
            var dcValues = new List<double?>();
            var userValues = new List<double?>();
            
            foreach (var site in siteCodes) {
                var dcRow = trendRows.FirstOrDefault(r => 
                    (((IDictionary<string, object>)r)["site_code"]?.ToString() ?? "Unknown") == site &&
                    (((IDictionary<string, object>)r)["category"]?.ToString() ?? "UPS DC") == "UPS DC");
                    
                var userRow = trendRows.FirstOrDefault(r => 
                    (((IDictionary<string, object>)r)["site_code"]?.ToString() ?? "Unknown") == site &&
                    (((IDictionary<string, object>)r)["category"]?.ToString() ?? "UPS DC") == "UPS USER");

                if (dcRow != null) {
                    var val = ((IDictionary<string, object>)dcRow)[metric];
                    dcValues.Add(val != null && val != DBNull.Value ? Math.Round(Convert.ToDouble(val), 1) : (double?)null);
                } else {
                    dcValues.Add(null);
                }
                
                if (userRow != null) {
                    var val = ((IDictionary<string, object>)userRow)[metric];
                    userValues.Add(val != null && val != DBNull.Value ? Math.Round(Convert.ToDouble(val), 1) : (double?)null);
                } else {
                    userValues.Add(null);
                }
            }
            
            var devicesList = new List<object> {
                new { id = "UPS DC", name = "UPS DC", values = dcValues },
                new { id = "UPS USER", name = "UPS USER", values = userValues }
            };
            
            trendsPerMetric[metric] = new {
                labels = siteCodes,
                devices = devicesList
            };
        }

        return new {
            meta = new {
                generated_at = DateTime.UtcNow.ToString("O"),
                period_label = periodLabel,
                total_devices = siteCodes.Count,
                resolution,
                aggregation,
                device_phase_types = new Dictionary<string, string>(),
                device_names = new Dictionary<string, string>(),
                dcpi_site_score = 0.0,
                dcpi_devices = new Dictionary<string, double>()
            },
            stats = new {}, 
            trends = trendsPerMetric,
            comparison = new {
                labels = new List<string>(),
                input = new List<string>(),
                output = new List<string>()
            }
        };
    }

    private async Task<object> BuildSensorReportAsync(string site_id, string? date_from, string? date_to, string resolution = "daily")
    {
        var dateFrom = DateTime.Parse(date_from ?? DateTime.Now.ToString("yyyy-MM-dd")).Date;
        var dateTo = DateTime.Parse(date_to ?? DateTime.Now.ToString("yyyy-MM-dd")).Date.AddDays(1).AddSeconds(-1);
        
        bool isAllSites = site_id == "ALL";
        Guid siteGuid = isAllSites ? Guid.Empty : Guid.Parse(site_id);
        
        var sqlDevices = @"
            SELECT DISTINCT d.id AS device_id, d.device_name
            FROM tbl_m_devices d
            LEFT JOIN tbl_r_asset_types at ON at.id = d.asset_type_id
            WHERE d.is_active = 1 
              AND (UPPER(at.type_name) LIKE '%SENSOR%' OR UPPER(d.device_name) LIKE '%SENSOR%')
        ";
        if (!isAllSites) sqlDevices += " AND d.site_id = @p0";
        sqlDevices += " ORDER BY d.device_name";

        var devices = isAllSites ? await _spExecutor.ExecuteSqlAsync(sqlDevices) : await _spExecutor.ExecuteSqlAsync(sqlDevices, new object[] { siteGuid });
        
        var deviceIds = new List<string>();
        var deviceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in devices)
        {
            var d = (IDictionary<string, object>)item;
            var did = d["device_id"]?.ToString() ?? "";
            deviceIds.Add(did);
            deviceNames[did] = d["device_name"]?.ToString() ?? did;
        }

        string periodLabel = $"{dateFrom:dd MMM} – {dateTo:dd MMM yyyy}";

        if (!deviceIds.Any() && !isAllSites)
        {
            return new { trends = new Dictionary<string, object>(), meta = new { total_devices = 0, period_label = periodLabel, device_names = deviceNames, dcpi_site_score = 0.0, dcpi_devices = new Dictionary<string, double>() } };
        }

        var keys = new List<string>();
        var labels = new List<string>();
        var cursor = dateFrom;
        while (cursor <= dateTo)
        {
            if (resolution == "daily")
            {
                keys.Add(cursor.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture));
                labels.Add(cursor.ToString("HH:00", CultureInfo.InvariantCulture));
                cursor = cursor.AddHours(1);
            }
            else
            {
                keys.Add(cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                labels.Add(cursor.ToString("dd MMM", CultureInfo.InvariantCulture));
                cursor = cursor.AddDays(1);
            }
        }

        string timeFormat = resolution == "daily" ? "yyyy-MM-dd HH:00" : "yyyy-MM-dd";
        var sqlData = @"
            SELECT 
                l.device_id,
                l.device_time,
                FORMAT(l.device_time, @p0) as time_key,
                l.temperature,
                l.humidity
            FROM tbl_t_sensor_logs l
            WHERE l.device_time >= @p1 AND l.device_time <= @p2
        ";

        if (!isAllSites && deviceIds.Any())
        {
            var inClause = string.Join(",", deviceIds.Select(id => $"'{id}'"));
            sqlData += $" AND l.device_id IN ({inClause})";
        }
        sqlData += " ORDER BY l.device_time";

        var rawData = await _spExecutor.ExecuteSqlAsync(sqlData, new object[] { timeFormat, dateFrom, dateTo });

        var tempDict = new Dictionary<string, Dictionary<string, List<double>>>(StringComparer.OrdinalIgnoreCase);
        var humidDict = new Dictionary<string, Dictionary<string, List<double>>>(StringComparer.OrdinalIgnoreCase);
        var devTimestamps = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);

        var allTargetIds = isAllSites ? new List<string> { "ALL" } : deviceIds;
        foreach (var did in allTargetIds)
        {
            tempDict[did] = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
            humidDict[did] = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
            devTimestamps[did] = new List<DateTime>();
        }

        foreach (var item in rawData)
        {
            var row = (IDictionary<string, object>)item;
            var did = row["device_id"]?.ToString() ?? "";
            if (!isAllSites && !tempDict.ContainsKey(did)) continue;
            var targetDid = isAllSites ? "ALL" : did;

            DateTime dt;
            if (row["device_time"] is DateTime dtVal) dt = dtVal;
            else if (!DateTime.TryParse(row["device_time"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) continue;

            devTimestamps[targetDid].Add(dt);

            string key = resolution == "daily" 
                ? dt.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture)
                : dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            if (row["temperature"] != null && row["temperature"] != DBNull.Value)
            {
                if (!tempDict[targetDid].ContainsKey(key)) tempDict[targetDid][key] = new List<double>();
                tempDict[targetDid][key].Add(Convert.ToDouble(row["temperature"]));
            }
            if (row["humidity"] != null && row["humidity"] != DBNull.Value)
            {
                if (!humidDict[targetDid].ContainsKey(key)) humidDict[targetDid][key] = new List<double>();
                humidDict[targetDid][key].Add(Convert.ToDouble(row["humidity"]));
            }
        }

        var tempDevices = new List<object>();
        var humidDevices = new List<object>();

        if (isAllSites)
        {
            var tVals = keys.Select(k => tempDict["ALL"].ContainsKey(k) && tempDict["ALL"][k].Count > 0 ? (double?)Math.Round(tempDict["ALL"][k].Average(), 1) : null).ToList();
            var hVals = keys.Select(k => humidDict["ALL"].ContainsKey(k) && humidDict["ALL"][k].Count > 0 ? (double?)Math.Round(humidDict["ALL"][k].Average(), 1) : null).ToList();
            tempDevices.Add(new { id = "ALL", name = "Global Average", values = tVals });
            humidDevices.Add(new { id = "ALL", name = "Global Average", values = hVals });
        }
        else
        {
            foreach (var did in deviceIds)
            {
                var name = deviceNames[did];
                var tVals = keys.Select(k => tempDict.ContainsKey(did) && tempDict[did].ContainsKey(k) && tempDict[did][k].Count > 0 ? (double?)Math.Round(tempDict[did][k].Average(), 1) : null).ToList();
                var hVals = keys.Select(k => humidDict.ContainsKey(did) && humidDict[did].ContainsKey(k) && humidDict[did][k].Count > 0 ? (double?)Math.Round(humidDict[did][k].Average(), 1) : null).ToList();
                tempDevices.Add(new { id = did, name = name, values = tVals });
                humidDevices.Add(new { id = did, name = name, values = hVals });
            }
        }

        // Calculate DCPI for Sensor Suhu
        var totalMinutes = (dateTo - dateFrom).TotalMinutes;
        var totalBuckets = (int)Math.Max(1, Math.Ceiling(totalMinutes / 5.0));

        var deviceDcpis = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double siteDcpiSum = 0;
        int siteDcpiCount = 0;

        if (!isAllSites)
        {
        // Preliminary pass to calculate Site Average PA for PAC
        double siteAvailSum = 0;
        int siteAvailCount = 0;
        var deviceAvailabilities = new Dictionary<string, double>();

        foreach (var did in deviceIds)
        {
            double availability = 0;
            if (devTimestamps.ContainsKey(did) && devTimestamps[did].Any())
            {
                var activeBuckets = new HashSet<int>();
                foreach (var dt in devTimestamps[did])
                {
                    if (dt >= dateFrom && dt <= dateTo)
                    {
                        var bIdx = (int)((dt - dateFrom).TotalMinutes / 5.0);
                        if (bIdx >= 0 && bIdx < totalBuckets) activeBuckets.Add(bIdx);
                    }
                }
                availability = Math.Min(100.0, Math.Round(((double)activeBuckets.Count / totalBuckets) * 100.0, 2));
            }
            deviceAvailabilities[did] = availability;
            if (did != "ALL")
            {
                siteAvailSum += availability;
                siteAvailCount++;
            }
        }
        double sitePacPa = siteAvailCount > 0 ? (siteAvailSum / siteAvailCount) : 0;

        foreach (var did in deviceIds)
        {
            double availability = deviceAvailabilities.ContainsKey(did) ? deviceAvailabilities[did] : 0;

            double temp = tempDict.ContainsKey(did) && tempDict[did].Any() ? tempDict[did].SelectMany(x => x.Value).DefaultIfEmpty(0).Average() : 0;
            double humid = humidDict.ContainsKey(did) && humidDict[did].Any() ? humidDict[did].SelectMany(x => x.Value).DefaultIfEmpty(0).Average() : 0;

            double dcpi = DcpiCalculator.CalculatePacDcpi(sitePacPa, humid, temp);
            deviceDcpis[did] = dcpi;
            if (did != "ALL")
            {
                siteDcpiSum += dcpi;
                siteDcpiCount++;
            }
        }
        }

        var trends = new Dictionary<string, object>();
        trends["ambient_temp"] = new { labels = labels, devices = tempDevices };
        trends["humidity"] = new { labels = labels, devices = humidDevices };

        return new {
            trends = trends,
            meta = new {
                total_devices = tempDevices.Count,
                period_label = periodLabel,
                resolution = resolution,
                device_names = deviceNames,
                dcpi_site_score = isAllSites ? 0 : (siteDcpiCount > 0 ? Math.Round(siteDcpiSum / siteDcpiCount, 2) : 0),
                dcpi_devices = deviceDcpis
            }
        };
    }

    private async Task<object> BuildPacReportAsync(string site_id, string? date_from, string? date_to, string resolution = "daily")
    {
        var dateFrom = DateTime.Parse(date_from ?? DateTime.Now.ToString("yyyy-MM-dd")).Date;
        var dateTo = DateTime.Parse(date_to ?? DateTime.Now.ToString("yyyy-MM-dd")).Date.AddDays(1).AddSeconds(-1);
        
        bool isAllSites = site_id == "ALL";
        Guid siteGuid = isAllSites ? Guid.Empty : Guid.Parse(site_id);
        
        var sqlDevices = @"
            SELECT DISTINCT d.id AS device_id, d.device_name
            FROM tbl_m_devices d
            LEFT JOIN tbl_r_asset_types at ON at.id = d.asset_type_id
            WHERE d.is_active = 1 
              AND UPPER(at.type_name) = 'PAC'
        ";
        if (!isAllSites) sqlDevices += " AND d.site_id = @p0";
        sqlDevices += " ORDER BY d.device_name";

        var devices = isAllSites ? await _spExecutor.ExecuteSqlAsync(sqlDevices) : await _spExecutor.ExecuteSqlAsync(sqlDevices, new object[] { siteGuid });
        
        var deviceIds = new List<string>();
        var deviceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in devices)
        {
            var d = (IDictionary<string, object>)item;
            var did = d["device_id"]?.ToString() ?? "";
            deviceIds.Add(did);
            deviceNames[did] = d["device_name"]?.ToString() ?? did;
        }

        string periodLabel = $"{dateFrom:dd MMM} – {dateTo:dd MMM yyyy}";

        if (!deviceIds.Any() && !isAllSites)
        {
            return new { trends = new Dictionary<string, object>(), meta = new { total_devices = 0, period_label = periodLabel, device_names = deviceNames, dcpi_site_score = 0.0, dcpi_devices = new Dictionary<string, double>() } };
        }

        var keys = new List<string>();
        var labels = new List<string>();
        var cursor = dateFrom;
        while(cursor <= dateTo) {
            if (resolution == "daily") {
                keys.Add(cursor.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture));
                labels.Add(cursor.ToString("HH:00", CultureInfo.InvariantCulture));
                cursor = cursor.AddHours(1);
            } else {
                keys.Add(cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                labels.Add(cursor.ToString("dd MMM", CultureInfo.InvariantCulture));
                cursor = cursor.AddDays(1);
            }
        }

        string timeFormat = resolution == "daily" ? "yyyy-MM-dd HH:00" : "yyyy-MM-dd";
        var sqlData = @"
            SELECT 
                device_id,
                device_time,
                FORMAT(device_time, @p0) as time_key,
                AVG(room_temp) as temp, AVG(room_humid) as humid,
                AVG(temp_setpoint) as temp_set, AVG(humid_setpoint) as humid_set,
                AVG(cool_demand) as cool, AVG(heat_demand) as heat,
                AVG(dehum_demand) as dehum, AVG(humid_demand) as humid_dem,
                AVG(valve_open) as valve
            FROM tbl_t_pac_logs
            WHERE device_time >= @p1 AND device_time <= @p2
        ";
        
        if (!isAllSites && deviceIds.Any()) {
            var inClause = string.Join(",", deviceIds.Select(id => $"'{id}'"));
            sqlData += $" AND device_id IN ({inClause})";
        }
        
        sqlData += " GROUP BY device_id, device_time, FORMAT(device_time, @p0) ORDER BY device_time";
        
        var rawData = await _spExecutor.ExecuteSqlAsync(sqlData, new object[] { timeFormat, dateFrom, dateTo });

        var metrics = new string[] { "temp", "temp_set", "humid", "humid_set", "cool", "heat", "dehum", "humid_dem", "valve" };
        var metricDict = new Dictionary<string, Dictionary<string, Dictionary<string, List<double>>>>(StringComparer.OrdinalIgnoreCase);
        var devTimestamps = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);

        var allTargetIds = isAllSites ? new List<string> { "ALL" } : deviceIds;
        foreach (var metric in metrics)
        {
            metricDict[metric] = new Dictionary<string, Dictionary<string, List<double>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var did in allTargetIds)
            {
                metricDict[metric][did] = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
            }
        }
        foreach (var did in allTargetIds) devTimestamps[did] = new List<DateTime>();

        foreach (var item in rawData)
        {
            var row = (IDictionary<string, object>)item;
            var did = row["device_id"]?.ToString() ?? "";
            if (!isAllSites && !metricDict["temp"].ContainsKey(did)) continue;
            var targetDid = isAllSites ? "ALL" : did;

            DateTime dt;
            if (row["device_time"] is DateTime dtVal) dt = dtVal;
            else if (!DateTime.TryParse(row["device_time"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) continue;

            devTimestamps[targetDid].Add(dt);

            string key = resolution == "daily" 
                ? dt.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture)
                : dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            foreach (var metric in metrics)
            {
                if (row[metric] != null && row[metric] != DBNull.Value)
                {
                    if (!metricDict[metric][targetDid].ContainsKey(key)) metricDict[metric][targetDid][key] = new List<double>();
                    metricDict[metric][targetDid][key].Add(Convert.ToDouble(row[metric]));
                }
            }
        }

        var trends = new Dictionary<string, object>();
        foreach (var metric in metrics)
        {
            var devs = new List<object>();
            if (isAllSites)
            {
                var vals = keys.Select(k => metricDict[metric]["ALL"].ContainsKey(k) && metricDict[metric]["ALL"][k].Count > 0 ? (double?)Math.Round(metricDict[metric]["ALL"][k].Average(), 1) : null).ToList();
                devs.Add(new { id = "ALL", name = "Global Average", values = vals });
            }
            else
            {
                foreach (var did in deviceIds)
                {
                    var devName = deviceNames[did];
                    var vals = keys.Select(k => metricDict[metric].ContainsKey(did) && metricDict[metric][did].ContainsKey(k) && metricDict[metric][did][k].Count > 0 ? (double?)Math.Round(metricDict[metric][did][k].Average(), 1) : null).ToList();
                    devs.Add(new { id = did, name = devName, values = vals });
                }
            }
            trends[metric] = new { labels = labels, devices = devs };
        }

        // Calculate DCPI for PAC
        var totalMinutes = (dateTo - dateFrom).TotalMinutes;
        var totalBuckets = (int)Math.Max(1, Math.Ceiling(totalMinutes / 5.0));

        var deviceDcpis = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double siteDcpiSum = 0;
        int siteDcpiCount = 0;

        // Preliminary pass to calculate Site Average PA for Sensor
        double siteAvailSum = 0;
        int siteAvailCount = 0;
        var deviceAvailabilities = new Dictionary<string, double>();

        foreach (var did in deviceIds)
        {
            double availability = 0;
            if (devTimestamps.ContainsKey(did) && devTimestamps[did].Any())
            {
                var activeBuckets = new HashSet<int>();
                foreach (var dt in devTimestamps[did])
                {
                    if (dt >= dateFrom && dt <= dateTo)
                    {
                        var bIdx = (int)((dt - dateFrom).TotalMinutes / 5.0);
                        if (bIdx >= 0 && bIdx < totalBuckets) activeBuckets.Add(bIdx);
                    }
                }
                availability = Math.Min(100.0, Math.Round(((double)activeBuckets.Count / totalBuckets) * 100.0, 2));
            }
            deviceAvailabilities[did] = availability;
            if (did != "ALL")
            {
                siteAvailSum += availability;
                siteAvailCount++;
            }
        }
        double sitePacPa = siteAvailCount > 0 ? (siteAvailSum / siteAvailCount) : 0;

        foreach (var did in deviceIds)
        {
            double availability = deviceAvailabilities.ContainsKey(did) ? deviceAvailabilities[did] : 0;

            double temp = metricDict["temp"].ContainsKey(did) && metricDict["temp"][did].Any() ? metricDict["temp"][did].SelectMany(x => x.Value).DefaultIfEmpty(0).Average() : 0;
            double humid = metricDict["humid"].ContainsKey(did) && metricDict["humid"][did].Any() ? metricDict["humid"][did].SelectMany(x => x.Value).DefaultIfEmpty(0).Average() : 0;

            double dcpi = DcpiCalculator.CalculatePacDcpi(sitePacPa, humid, temp);
            deviceDcpis[did] = dcpi;
            if (did != "ALL")
            {
                siteDcpiSum += dcpi;
                siteDcpiCount++;
            }
        }

        return new {
            trends = trends,
            meta = new {
                total_devices = isAllSites ? 1 : deviceIds.Count,
                period_label = periodLabel,
                resolution = resolution,
                device_names = deviceNames,
                dcpi_site_score = isAllSites ? 0 : (siteDcpiCount > 0 ? Math.Round(siteDcpiSum / siteDcpiCount, 2) : 0),
                dcpi_devices = deviceDcpis
            }
        };
    }

    private async Task<object> BuildCctvReportAsync(string site_id, string? date_from, string? date_to, string resolution = "daily")
    {
        var dateFrom = DateTime.Parse(date_from ?? DateTime.Now.ToString("yyyy-MM-dd")).Date;
        var dateTo = DateTime.Parse(date_to ?? DateTime.Now.ToString("yyyy-MM-dd")).Date.AddDays(1).AddSeconds(-1);
        
        bool isAllSites = site_id == "ALL";
        Guid siteGuid = isAllSites ? Guid.Empty : Guid.Parse(site_id);
        
        var sqlDevices = @"
            SELECT DISTINCT d.id AS device_id, d.device_name
            FROM tbl_m_devices d
            LEFT JOIN tbl_r_asset_types at ON at.id = d.asset_type_id
            WHERE d.is_active = 1 
              AND UPPER(at.type_name) = 'CCTV'
        ";
        if (!isAllSites) sqlDevices += " AND d.site_id = @p0";
        sqlDevices += " ORDER BY d.device_name";

        var devices = isAllSites ? await _spExecutor.ExecuteSqlAsync(sqlDevices) : await _spExecutor.ExecuteSqlAsync(sqlDevices, new object[] { siteGuid });
        
        var deviceIds = new List<string>();
        var deviceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in devices)
        {
            var d = (IDictionary<string, object>)item;
            var did = d["device_id"]?.ToString() ?? "";
            deviceIds.Add(did);
            deviceNames[did] = d["device_name"]?.ToString() ?? did;
        }

        string periodLabel = $"{dateFrom:dd MMM} – {dateTo:dd MMM yyyy}";

        if (!deviceIds.Any() && !isAllSites)
        {
            return new {
                meta = new { total_devices = 0, period_label = periodLabel, device_names = deviceNames, dcpi_site_score = 0.0, dcpi_devices = new Dictionary<string, double>() },
                stats = new { total_records = 0, uptime_duration = "0m", downtime_duration = "0m", latest_status = "Offline", latest_status_text = "OFFLINE" },
                trends = new { timeline = new { labels = new string[0], devices = new object[0] }, comparison = new { device_names = new string[0], uptime_minutes = new double[0], downtime_minutes = new double[0] } }
            };
        }

        var sqlData = @"
            SELECT 
                l.device_id,
                l.device_time,
                l.status
            FROM tbl_t_cctv_logs l
            WHERE l.device_time >= @p0 AND l.device_time <= @p1
        ";
        if (!isAllSites && deviceIds.Any())
        {
            var inClause = string.Join(",", deviceIds.Select(id => $"'{id}'"));
            sqlData += $" AND l.device_id IN ({inClause})";
        }
        sqlData += " ORDER BY l.device_time ASC";

        var rawData = await _spExecutor.ExecuteSqlAsync(sqlData, new object[] { dateFrom, dateTo });

        var keys = new List<string>();
        var labels = new List<string>();
        var cursor = dateFrom;
        while (cursor <= dateTo)
        {
            if (resolution == "daily")
            {
                keys.Add(cursor.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture));
                labels.Add(cursor.ToString("HH:00", CultureInfo.InvariantCulture));
                cursor = cursor.AddHours(1);
            }
            else
            {
                keys.Add(cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                labels.Add(cursor.ToString("dd MMM", CultureInfo.InvariantCulture));
                cursor = cursor.AddDays(1);
            }
        }

        // Timeline state (1 = Online, 0 = Offline)
        var timelineDict = new Dictionary<string, Dictionary<string, List<double>>>(StringComparer.OrdinalIgnoreCase);
        var devTimestamps = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);
        var devOnlineTimestamps = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);

        var allTargetIds = isAllSites ? new List<string> { "ALL" } : deviceIds;
        foreach (var did in allTargetIds)
        {
            timelineDict[did] = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
            devTimestamps[did] = new List<DateTime>();
            devOnlineTimestamps[did] = new List<DateTime>();
        }

        int totalRecords = 0;
        string latestStatus = "Offline";

        foreach (var item in rawData)
        {
            var row = (IDictionary<string, object>)item;
            var did = row["device_id"]?.ToString() ?? "";
            if (!isAllSites && !timelineDict.ContainsKey(did)) continue;
            var targetDid = isAllSites ? "ALL" : did;

            DateTime dt;
            if (row["device_time"] is DateTime dtVal) dt = dtVal;
            else if (!DateTime.TryParse(row["device_time"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) continue;

            string statusStr = row["status"]?.ToString() ?? "Offline";
            bool isOnline = statusStr.Equals("Online", StringComparison.OrdinalIgnoreCase) || statusStr == "1";

            devTimestamps[targetDid].Add(dt);
            if (isOnline) devOnlineTimestamps[targetDid].Add(dt);

            string key = resolution == "daily"
                ? dt.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture)
                : dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            if (!timelineDict[targetDid].ContainsKey(key)) timelineDict[targetDid][key] = new List<double>();
            timelineDict[targetDid][key].Add(isOnline ? 1.0 : 0.0);

            totalRecords++;
            latestStatus = isOnline ? "Online" : "Offline";
        }

        var trendDevices = new List<object>();
        if (isAllSites)
        {
            var vals = keys.Select(k => timelineDict["ALL"].ContainsKey(k) && timelineDict["ALL"][k].Count > 0 ? (double?)Math.Round(timelineDict["ALL"][k].Average(), 2) : null).ToList();
            trendDevices.Add(new { id = "ALL", name = "Global Status", values = vals });
        }
        else
        {
            foreach (var did in deviceIds)
            {
                var name = deviceNames[did];
                var vals = keys.Select(k => timelineDict.ContainsKey(did) && timelineDict[did].ContainsKey(k) && timelineDict[did][k].Count > 0 ? (double?)Math.Round(timelineDict[did][k].Average(), 2) : null).ToList();
                trendDevices.Add(new { id = did, name = name, values = vals });
            }
        }

        // Calculate Duration & DCPI for CCTV
        var totalMinutes = (dateTo - dateFrom).TotalMinutes;
        var totalBuckets = (int)Math.Max(1, Math.Ceiling(totalMinutes / 5.0));

        var devNames = new List<string>();
        var devUptimeMins = new List<double>();
        var devDowntimeMins = new List<double>();
        var devUptimeLabels = new List<string>();
        var devDowntimeLabels = new List<string>();

        var deviceDcpis = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double siteDcpiSum = 0;
        int siteDcpiCount = 0;

        foreach (var did in deviceIds)
        {
            double availability = 0;
            double upMins = 0;
            double downMins = totalMinutes;

            if (devOnlineTimestamps.ContainsKey(did) && devOnlineTimestamps[did].Any())
            {
                var activeBuckets = new HashSet<int>();
                foreach (var dt in devOnlineTimestamps[did])
                {
                    if (dt >= dateFrom && dt <= dateTo)
                    {
                        var bIdx = (int)((dt - dateFrom).TotalMinutes / 5.0);
                        if (bIdx >= 0 && bIdx < totalBuckets) activeBuckets.Add(bIdx);
                    }
                }
                availability = Math.Min(100.0, Math.Round(((double)activeBuckets.Count / totalBuckets) * 100.0, 2));
                upMins = activeBuckets.Count * 5.0;
                downMins = Math.Max(0, totalMinutes - upMins);
            }

            double dcpi = DcpiCalculator.CalculateCctvDcpi(availability);
            deviceDcpis[did] = dcpi;
            siteDcpiSum += dcpi;
            siteDcpiCount++;

            devNames.Add(deviceNames[did]);
            devUptimeMins.Add(Math.Round(upMins, 1));
            devDowntimeMins.Add(Math.Round(downMins, 1));
            devUptimeLabels.Add(FormatMinutes(upMins));
            devDowntimeLabels.Add(FormatMinutes(downMins));
        }

        double totalUpMins = devUptimeMins.DefaultIfEmpty(0).Sum();
        double totalDownMins = devDowntimeMins.DefaultIfEmpty(0).Sum();

        return new {
            meta = new {
                generated_at = DateTime.UtcNow.ToString("O"),
                period_label = periodLabel,
                total_devices = isAllSites ? 1 : deviceIds.Count,
                resolution = resolution,
                device_names = deviceNames,
                dcpi_site_score = isAllSites ? 0 : (siteDcpiCount > 0 ? Math.Round(siteDcpiSum / siteDcpiCount, 2) : 0),
                dcpi_devices = deviceDcpis
            },
            stats = new {
                total_records = totalRecords,
                uptime_duration = FormatMinutes(totalUpMins),
                downtime_duration = FormatMinutes(totalDownMins),
                latest_status = latestStatus,
                latest_status_text = latestStatus.ToUpper()
            },
            trends = new {
                timeline = new {
                    labels = labels,
                    devices = trendDevices,
                    unit = "Status (1=ON, 0=OFF)"
                },
                comparison = new {
                    device_names = devNames,
                    uptime_minutes = devUptimeMins,
                    downtime_minutes = devDowntimeMins,
                    uptime_labels = devUptimeLabels,
                    downtime_labels = devDowntimeLabels
                }
            }
        };
    }

    private static string FormatMinutes(double minutes)
    {
        if (minutes <= 0) return "0m";
        var ts = TimeSpan.FromMinutes(minutes);
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{ts.Minutes}m";
    }
}

public static class DcpiCalculator
{
    public static double CalculateUpsDcpi(double availability, double freqIn, double freqOut, double voltIn, double voltOut, double loadScore, double runtimeScore)
    {
        var a = GetScoreAvailability(availability);
        var fi = GetScoreFreq(freqIn);
        var fo = GetScoreFreq(freqOut);
        var vi = GetScoreVolt(voltIn);
        var vo = GetScoreVolt(voltOut);
        return Math.Round((a + fi + fo + vi + vo + loadScore + runtimeScore) / 7.0, 2);
    }

    public static double CalculatePacDcpi(double pacSiteAvailability, double humidity, double temp)
    {
        var a = GetScoreAvailability(pacSiteAvailability);
        var h = GetScoreHumidity(humidity);
        var t = GetScoreTemp(temp);
        return Math.Round((a + h + t) / 3.0, 2);
    }

    public static double CalculateCctvDcpi(double availability)
    {
        return Math.Round(GetScoreAvailability(availability), 2);
    }

    public static double GetScoreAvailability(double x)
    {
        if (x >= 100.0) return 3; // Excel Note: "Untuk Physical Avability (%) Pada UPS,PAC,CCTV ,jika 100% nilainya 3"
        if (x > 99.4 && x < 100.0) return 5;
        if (x > 98.0 && x <= 99.4) return 4;
        if (x > 96.9 && x <= 98.0) return 3;
        if (x > 95.0 && x <= 96.9) return 2;
        return 1; // x <= 95%
    }

    public static double GetScoreFreq(double x)
    {
        if (x <= 45.0 || x >= 52.1) return 1;
        if ((x > 47.9 && x <= 48.4) || (x >= 51.6 && x < 52.1)) return 2;
        if ((x > 48.4 && x <= 48.9) || (x >= 51.1 && x < 51.6)) return 3;
        if ((x > 48.9 && x <= 49.9) || (x >= 50.1 && x < 51.1)) return 4;
        if (x > 49.9 && x < 50.1) return 5;
        return 1;
    }

    public static double GetScoreVolt(double x)
    {
        if (x <= 209.0 || x >= 231.0) return 1;
        if ((x > 209.0 && x <= 215.0) || (x >= 225.0 && x < 231.0)) return 2;
        if ((x > 215.0 && x <= 217.0) || (x >= 223.0 && x < 225.0)) return 3;
        if ((x > 217.0 && x <= 219.0) || (x >= 221.0 && x < 223.0)) return 4;
        if (x > 219.0 && x < 221.0) return 5;
        return 1;
    }

    public static double GetScoreHumidity(double x)
    {
        if ((x >= 0.0 && x <= 29.9) || (x >= 70.1 && x <= 100.0)) return 1;
        if ((x > 29.9 && x <= 34.9) || (x >= 65.1 && x < 70.1)) return 2;
        if ((x > 34.9 && x <= 39.9) || (x >= 60.1 && x < 65.1)) return 3;
        if ((x > 39.9 && x <= 47.9) || (x >= 53.1 && x < 60.1)) return 4;
        if (x > 47.9 && x < 53.1) return 5;
        return 1;
    }

    public static double GetScoreTemp(double x)
    {
        if ((x >= 0.0 && x <= 16.0) || (x >= 25.0 && x <= 50.0)) return 1;
        if ((x > 16.0 && x <= 17.5) || (x >= 22.1 && x < 25.0)) return 2;
        if ((x > 17.5 && x <= 17.9) || (x >= 21.6 && x < 22.1)) return 3;
        if ((x > 17.9 && x <= 19.4) || (x >= 20.6 && x < 21.6)) return 4;
        if (x > 19.4 && x < 20.6) return 5;
        return 1;
    }

    public static double GetScoreLoad(double utilizationPct, double readinessTotalLoadPct, int numUpsInSite)
    {
        // Utilization Index
        double ut = 1;
        if (utilizationPct <= 10 || utilizationPct >= 90) ut = 1;
        else if ((utilizationPct > 10 && utilizationPct <= 20) || (utilizationPct > 80 && utilizationPct <= 90)) ut = 2;
        else if ((utilizationPct > 20 && utilizationPct <= 30) || (utilizationPct > 70 && utilizationPct <= 80)) ut = 3;
        else if ((utilizationPct > 30 && utilizationPct <= 40) || (utilizationPct > 60 && utilizationPct <= 70)) ut = 4;
        else if (utilizationPct > 40 && utilizationPct <= 60) ut = 5;

        // Readiness Index
        double rd = 1;
        if (numUpsInSite <= 1 || readinessTotalLoadPct > 80) rd = 1; // Tidak ada redundancy power atau load >= 80%
        else if (readinessTotalLoadPct > 70 && readinessTotalLoadPct <= 80) rd = 2;
        else if (readinessTotalLoadPct > 60 && readinessTotalLoadPct <= 70) rd = 3;
        else if (readinessTotalLoadPct > 50 && readinessTotalLoadPct <= 60) rd = 4;
        else if (readinessTotalLoadPct <= 50) rd = 5;

        return (ut * 0.40) + (rd * 0.60);
    }

    public static double GetScoreRuntime(double actualRuntimeMinutes, double targetRuntimeMinutes = 35.0)
    {
        double x = (actualRuntimeMinutes / targetRuntimeMinutes) * 100.0;
        if (x < 70) return 1;
        if (x >= 70 && x < 80) return 2;
        if (x >= 80 && x < 90) return 3;
        if (x >= 90 && x < 100) return 4;
        return 5;
    }
}
