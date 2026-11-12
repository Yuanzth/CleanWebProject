using InfraHub.Core.Interfaces;
using InfraHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace InfraHub.Web.Areas.Tracking.Controllers;

[Area("Tracking")]
[Authorize]
[Route("Tracking/Api/[action]")]
public class TrackingApiController : Controller
{
    private readonly IStoredProcedureExecutor _spExecutor;
    private readonly AppDbContext _context;

    public TrackingApiController(IStoredProcedureExecutor spExecutor, AppDbContext context)
    {
        _spExecutor = spExecutor;
        _context = context;
    }

    private string? NormalizePath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        path = path.Replace("\\", "/");
        
        // Remove leading slash if exists to standardize check
        if (path.StartsWith("/")) path = path.Substring(1);
        
        if (!path.StartsWith("storage/")) 
        {
            path = "storage/" + path;
        }
        
        return "/" + path;
    }

    private static object? GetDictValue(IDictionary<string, object>? dict, string key)
    {
        if (dict == null) return null;
        if (dict.TryGetValue(key, out var val)) return val;
        var kvp = dict.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));
        return kvp.Key != null ? kvp.Value : null;
    }

    private static string GetString(IDictionary<string, object>? dict, string key, string defaultValue = "")
    {
        var val = GetDictValue(dict, key);
        if (val == null || val == DBNull.Value) return defaultValue;
        return val.ToString() ?? defaultValue;
    }

    private static double GetDouble(IDictionary<string, object>? dict, string key, double defaultValue = 0.0)
    {
        var val = GetDictValue(dict, key);
        if (val == null || val == DBNull.Value) return defaultValue;
        try { return Convert.ToDouble(val); } catch { return defaultValue; }
    }

    private static double? GetNullableDouble(IDictionary<string, object>? dict, string key)
    {
        var val = GetDictValue(dict, key);
        if (val == null || val == DBNull.Value) return null;
        try { return Convert.ToDouble(val); } catch { return null; }
    }

    private static int GetInt(IDictionary<string, object>? dict, string key, int defaultValue = 0)
    {
        var val = GetDictValue(dict, key);
        if (val == null || val == DBNull.Value) return defaultValue;
        try { return Convert.ToInt32(val); } catch { return defaultValue; }
    }

    private static (DateTime expireAt, object response)? _sitesCache = null;
    private static readonly object _cacheLock = new object();

    [HttpGet]
    public async Task<IActionResult> GetSitesWithUnits()
    {
        try
        {
            lock (_cacheLock)
            {
                if (_sitesCache.HasValue && DateTime.UtcNow < _sitesCache.Value.expireAt)
                {
                    return Json(_sitesCache.Value.response);
                }
            }

            var parameters = new Dictionary<string, object?> { { "UserID", null }, { "OnlyActiveSites", 1 } };
            var rawData = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetTrackingSitesWithUnits", parameters);

            // Fetch active maps and boundaries from DB
            var sitesInfo = await _context.Sites
                .Include(s => s.SiteBoundaries.Where(b => b.IsActive))
                .Include(s => s.SiteBasemaps.Where(b => b.IsActive))
                .Include(s => s.SiteCoverages.Where(c => c.IsActive))
                .ToDictionaryAsync(s => s.Id);

            var sitesMap = new Dictionary<string, object>();

            foreach (var row in rawData)
            {
                var siteCode = row.site_code?.ToString() ?? "";
                if (!sitesMap.ContainsKey(siteCode))
                {
                    Guid? parsedSiteId = null;
                    if (row.site_id != null)
                    {
                        if (Guid.TryParse(row.site_id.ToString(), out Guid sid))
                        {
                            parsedSiteId = sid;
                        }
                    }

                    object? polygonData = null;
                    string? basemapUrl = null;
                    string? coverageUrl = null;

                    if (parsedSiteId.HasValue && sitesInfo.TryGetValue(parsedSiteId.Value, out var siteEntity))
                    {
                        var bound = siteEntity.SiteBoundaries.FirstOrDefault();
                        if (bound != null && !string.IsNullOrEmpty(bound.PolygonData))
                        {
                            try
                            {
                                polygonData = System.Text.Json.JsonSerializer.Deserialize<object>(bound.PolygonData);
                            }
                            catch
                            {
                                polygonData = null;
                            }
                        }

                        var baseM = siteEntity.SiteBasemaps.FirstOrDefault();
                        if (baseM != null && !string.IsNullOrEmpty(baseM.FilePath))
                        {
                            basemapUrl = NormalizePath(baseM.FilePath);
                        }

                        var covM = siteEntity.SiteCoverages.FirstOrDefault();
                        if (covM != null && !string.IsNullOrEmpty(covM.FilePath))
                        {
                            coverageUrl = NormalizePath(covM.FilePath);
                        }
                    }

                    sitesMap[siteCode] = new
                    {
                        code = siteCode,
                        name = row.site_name?.ToString() ?? siteCode,
                        latitude = row.site_latitude != null ? Convert.ToDouble(row.site_latitude) : (double?)null,
                        longitude = row.site_longitude != null ? Convert.ToDouble(row.site_longitude) : (double?)null,
                        polygon = polygonData,
                        basemap = basemapUrl,
                        coverage = coverageUrl,
                        units = new List<object>()
                    };
                }

                if (row.device_id != null)
                {
                    var rowDict = row as IDictionary<string, object>;
                    var lat = Convert.ToDouble(rowDict.ContainsKey("current_latitude") ? (rowDict["current_latitude"] ?? 0) : 0);
                    var lng = Convert.ToDouble(rowDict.ContainsKey("current_longitude") ? (rowDict["current_longitude"] ?? 0) : 0);
                    var gpsStr = rowDict.ContainsKey("gps_status") ? (rowDict["gps_status"]?.ToString() ?? "") : "";
                    var hasGpsErr = (lat == 0.0 && lng == 0.0) || string.Equals(gpsStr, "Not Fixed", StringComparison.OrdinalIgnoreCase) || string.Equals(gpsStr, "Error", StringComparison.OrdinalIgnoreCase);

                    var unit = new
                    {
                        id = rowDict.ContainsKey("device_id") ? rowDict["device_id"] : null,
                        name = rowDict.ContainsKey("device_name") ? rowDict["device_name"] : null,
                        serial_number = rowDict.ContainsKey("serial_number") ? rowDict["serial_number"] : null,
                        fleet_type = (rowDict.ContainsKey("fleet_type") ? rowDict["fleet_type"] : null) ?? "Heavy Duty Truck",
                        status = rowDict.ContainsKey("status") ? rowDict["status"] : null,
                        speed = Convert.ToInt32(rowDict.ContainsKey("speed_kmh") ? (rowDict["speed_kmh"] ?? 0) : 0),
                        gps = gpsStr,
                        has_gps_error = hasGpsErr,
                        satellite = Convert.ToInt32(rowDict.ContainsKey("satellite_in_use") ? (rowDict["satellite_in_use"] ?? 0) : 0),
                        heading = Convert.ToInt32(rowDict.ContainsKey("heading_degree") ? (rowDict["heading_degree"] ?? 0) : 0),
                        lat = Math.Round(lat, 6),
                        lng = Math.Round(lng, 6),
                        altitude = Convert.ToDouble(rowDict.ContainsKey("altitude") ? (rowDict["altitude"] ?? 0) : 0),
                        lastUpdate = rowDict.ContainsKey("last_update") && rowDict["last_update"] != null ? Convert.ToDateTime(rowDict["last_update"]).ToString("dd/MM/yyyy HH:mm T") : "-",
                        networkQuality = new
                        {
                            rssi = rowDict.ContainsKey("network_rssi") && rowDict["network_rssi"] != null ? (double?)Convert.ToDouble(rowDict["network_rssi"]) : null,
                            rsrp = rowDict.ContainsKey("network_rsrp") && rowDict["network_rsrp"] != null ? (double?)Convert.ToDouble(rowDict["network_rsrp"]) : null,
                            rsrq = rowDict.ContainsKey("network_rsrq") && rowDict["network_rsrq"] != null ? (double?)Convert.ToDouble(rowDict["network_rsrq"]) : null,
                            sinr = rowDict.ContainsKey("network_sinr") && rowDict["network_sinr"] != null ? (double?)Convert.ToDouble(rowDict["network_sinr"]) : null
                        }
                    };

                    ((List<object>)((dynamic)sitesMap[siteCode]).units).Add(unit);
                }
            }

            var finalResult = new
            {
                status = "success",
                message = "Tracking sites retrieved successfully",
                meta = new
                {
                    total_sites = sitesMap.Count,
                    total_units = sitesMap.Values.Sum(s => ((List<object>)((dynamic)s).units).Count),
                    timestamp = DateTime.UtcNow.ToString("s")
                },
                data = sitesMap.Values.ToList()
            };

            lock (_cacheLock)
            {
                _sitesCache = (DateTime.UtcNow.AddSeconds(15), finalResult);
            }

            return Json(finalResult);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = "Failed to retrieve tracking sites: " + ex.Message });
        }
    }

    [HttpGet("{deviceId}")]
    public async Task<IActionResult> GetTrackHistory(
        string deviceId, 
        [FromQuery] string? date, 
        [FromQuery(Name = "start_date")] string? startDate,
        [FromQuery(Name = "end_date")] string? endDate,
        [FromQuery] string time_start = "00:00:00", 
        [FromQuery] string time_end = "23:59:59")
    {
        try
        {
            var targetDate = !string.IsNullOrEmpty(date) ? date : (!string.IsNullOrEmpty(startDate) ? startDate : DateTime.Now.ToString("yyyy-MM-dd"));
            var targetEndDate = !string.IsNullOrEmpty(endDate) ? endDate : targetDate;
                
            var parameters = new Dictionary<string, object?> 
            { 
                { "DeviceID", deviceId },
                { "DateFrom", targetDate },
                { "DateTo", targetEndDate },
                { "TimeFrom", time_start },
                { "TimeTo", time_end }
            };
            
            var trackPoints = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetTrackingHistory", parameters);
            
            var posParameters = new Dictionary<string, object?> { { "DeviceID", deviceId } };
            var deviceInfo = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetCurrentTrackingPosition", posParameters);

            if (!deviceInfo.Any())
                return NotFound(new { status = "error", message = "Device not found" });

            var device = deviceInfo.First();
            var deviceDict = device as IDictionary<string, object>;
            var polylinePoints = new List<double[]>();
            var rawPoints = new List<object>();

            foreach(var point in trackPoints)
            {
                var ptDict = point as IDictionary<string, object>;
                if (ptDict == null) continue;

                var lat = GetDouble(ptDict, "latitude");
                var lng = GetDouble(ptDict, "longitude");
                var gpsStatus = GetString(ptDict, "gps_status", "Unknown");

                // Filter out invalid 0,0 coordinates or Not Fixed GPS status from track history
                if ((lat == 0.0 && lng == 0.0) || string.Equals(gpsStatus, "Not Fixed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                lat = Math.Round(lat, 6);
                lng = Math.Round(lng, 6);

                var devTimeObj = GetDictValue(ptDict, "device_time");
                var devTime = devTimeObj != null && devTimeObj != DBNull.Value ? Convert.ToDateTime(devTimeObj).ToString("yyyy-MM-ddTHH\\:mm\\:ss") : null;

                polylinePoints.Add(new double[] { lat, lng });
                rawPoints.Add(new
                {
                    lat = lat,
                    lng = lng,
                    speed = GetInt(ptDict, "speed_kmh"),
                    heading = GetInt(ptDict, "heading_degree"),
                    gps_status = gpsStatus,
                    network_rssi = GetNullableDouble(ptDict, "network_rssi"),
                    network_rsrp = GetNullableDouble(ptDict, "network_rsrp"),
                    network_rsrq = GetNullableDouble(ptDict, "network_rsrq"),
                    network_sinr = GetNullableDouble(ptDict, "network_sinr"),
                    time = devTime
                });
            }

            var sumParameters = new Dictionary<string, object?> 
            { 
                { "DeviceID", deviceId },
                { "DateFrom", date },
                { "DateTo", date }
            };
            var statsResult = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetTrackingSummary", sumParameters);
            var stats = statsResult.FirstOrDefault();
            var statsDict = stats as IDictionary<string, object>;

            var siteCodeStr = deviceDict?.ContainsKey("site_code") == true ? deviceDict["site_code"]?.ToString() : null;
            object? polygonData = null;
            string? basemapUrl = null;
            string? coverageUrl = null;

            if (!string.IsNullOrEmpty(siteCodeStr))
            {
                var siteEntity = await _context.Sites
                    .Include(s => s.SiteBoundaries.Where(b => b.IsActive))
                    .Include(s => s.SiteBasemaps.Where(b => b.IsActive))
                    .Include(s => s.SiteCoverages.Where(c => c.IsActive))
                    .FirstOrDefaultAsync(s => s.SiteCode == siteCodeStr);

                if (siteEntity != null)
                {
                    var bound = siteEntity.SiteBoundaries.FirstOrDefault();
                    if (bound != null && !string.IsNullOrEmpty(bound.PolygonData))
                    {
                        try { polygonData = System.Text.Json.JsonSerializer.Deserialize<object>(bound.PolygonData); } catch { polygonData = null; }
                    }

                    var baseM = siteEntity.SiteBasemaps.FirstOrDefault();
                    if (baseM != null) basemapUrl = NormalizePath(baseM.FilePath);

                    var cov = siteEntity.SiteCoverages.FirstOrDefault();
                    if (cov != null) coverageUrl = NormalizePath(cov.FilePath);
                }
            }

            return Json(new
            {
                status = "success",
                message = "Track history retrieved successfully",
                meta = new
                {
                    date_range = $"{date} to {date}",
                    device_id = deviceId,
                    total_points = polylinePoints.Count,
                    timestamp = DateTime.UtcNow.ToString("s")
                },
                device = new
                {
                    id = deviceDict?.ContainsKey("device_id") == true ? deviceDict["device_id"] : null,
                    name = deviceDict?.ContainsKey("device_name") == true ? deviceDict["device_name"] : null,
                    serial_number = deviceDict?.ContainsKey("serial_number") == true ? deviceDict["serial_number"] : null,
                    fleet_type = (deviceDict?.ContainsKey("fleet_type") == true ? deviceDict["fleet_type"] : null) ?? "Heavy Duty Truck",
                    site_code = deviceDict?.ContainsKey("site_code") == true ? deviceDict["site_code"] : null,
                    status = deviceDict?.ContainsKey("status") == true ? deviceDict["status"] : null,
                    polygon = polygonData,
                    basemap_url = basemapUrl,
                    coverage_url = coverageUrl
                },
                track_points = polylinePoints,
                raw_points = rawPoints,
                summary = statsDict != null ? new
                {
                    total_points = statsDict.ContainsKey("total_points") ? statsDict["total_points"] : 0,
                    total_distance_km = Convert.ToDouble(statsDict.ContainsKey("total_distance_km") ? (statsDict["total_distance_km"] ?? 0) : 0),
                    total_stops = Convert.ToInt32(statsDict.ContainsKey("total_stops") ? (statsDict["total_stops"] ?? 0) : 0),
                    idle_time_minutes = Math.Round(Convert.ToDouble(statsDict.ContainsKey("idle_time_seconds") ? (statsDict["idle_time_seconds"] ?? 0) : 0) / 60.0, 2),
                    avg_speed_kmh = Convert.ToDouble(statsDict.ContainsKey("avg_speed_kmh") ? (statsDict["avg_speed_kmh"] ?? 0) : 0),
                    max_speed_kmh = Convert.ToDouble(statsDict.ContainsKey("max_speed_kmh") ? (statsDict["max_speed_kmh"] ?? 0) : 0),
                    min_speed_kmh = Convert.ToDouble(statsDict.ContainsKey("min_speed_kmh") ? (statsDict["min_speed_kmh"] ?? 0) : 0),
                    hourly_stats = new List<object>() // Missing SP for hourly stats in original SQL but required
                } : null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = "Failed to retrieve track history: " + ex.Message });
        }
    }

    [HttpGet("{siteCode?}")]
    public async Task<IActionResult> GetSiteTrackHistory(
        string? siteCode, 
        [FromQuery(Name = "siteCode")] string? querySiteCode,
        [FromQuery] string? date, 
        [FromQuery(Name = "start_date")] string? startDate,
        [FromQuery(Name = "end_date")] string? endDate,
        [FromQuery] string time_start = "00:00:00", 
        [FromQuery] string time_end = "23:59:59")
    {
        try
        {
            var targetSiteCode = !string.IsNullOrEmpty(siteCode) ? siteCode : querySiteCode;
            if (string.IsNullOrEmpty(targetSiteCode))
            {
                return BadRequest(new { status = "error", message = "SiteCode parameter is required" });
            }

            var targetDate = !string.IsNullOrEmpty(date) ? date : (!string.IsNullOrEmpty(startDate) ? startDate : DateTime.Now.ToString("yyyy-MM-dd"));
            var targetEndDate = !string.IsNullOrEmpty(endDate) ? endDate : targetDate;
            time_start = string.IsNullOrEmpty(time_start) ? "00:00:00" : time_start;
            time_end = string.IsNullOrEmpty(time_end) ? "23:59:59" : time_end;

            DateTime dtStart = DateTime.TryParse($"{targetDate} {time_start}", out var ds) ? ds : DateTime.Today;
            DateTime dtEnd = DateTime.TryParse($"{targetEndDate} {time_end}", out var de) ? de : DateTime.Today.AddDays(1).AddSeconds(-1);

            // Step 1: Get active device IDs for this site (fast, ~5ms)
            var parameters = new Dictionary<string, object?> { { "UserID", null }, { "OnlyActiveSites", 1 } };
            var rawSites = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetTrackingSitesWithUnits", parameters);

            var deviceIds = new List<string>();
            var deviceIdToName = new Dictionary<string, string>();

            foreach (var row in rawSites)
            {
                var rowDict = row as IDictionary<string, object>;
                if (rowDict != null)
                {
                    var sc = GetString(rowDict, "site_code");
                    if (string.Equals(sc, targetSiteCode, StringComparison.OrdinalIgnoreCase))
                    {
                        var devId = GetString(rowDict, "device_id");
                        var devName = GetString(rowDict, "device_name", devId);
                        if (!string.IsNullOrEmpty(devId))
                        {
                            if (!deviceIds.Contains(devId)) deviceIds.Add(devId);
                            deviceIdToName[devId] = devName;
                        }
                    }
                }
            }

            if (!deviceIds.Any())
            {
                return Json(new { status = "success", site_code = targetSiteCode, total_points = 0, points = new List<object>() });
            }

            // Step 2: STREAMING — Stream JSON directly from SqlDataReader to HTTP Response.Body
            // Memory stays flat at ~2MB even for 500K+ points (vs 330MB+ loading all into List<object>)
            var connectionString = _context.Database.GetConnectionString();

            var inClause = new List<string>();
            for (int i = 0; i < deviceIds.Count; i++)
            {
                inClause.Add($"@p{i + 2}");
            }

            var bulkSql = $@"
                SELECT 
                    latitude, longitude, gps_status, device_id, device_time,
                    network_rsrp, network_rssi, network_rsrq, network_sinr
                FROM tbl_t_fleet_tracking_logs WITH (NOLOCK)
                WHERE device_time >= @p0
                  AND device_time <= @p1
                  AND device_id IN ({string.Join(",", inClause)})
                  AND latitude IS NOT NULL AND latitude <> 0 
                  AND longitude IS NOT NULL AND longitude <> 0
                  AND (gps_status IS NULL OR gps_status NOT IN ('Not Fixed', 'Error'))
                ORDER BY device_time
            ";

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(bulkSql, connection)
            {
                CommandType = CommandType.Text,
                CommandTimeout = 180
            };

            command.Parameters.AddWithValue("@p0", dtStart);
            command.Parameters.AddWithValue("@p1", dtEnd);
            for (int i = 0; i < deviceIds.Count; i++)
            {
                command.Parameters.AddWithValue($"@p{i + 2}", deviceIds[i]);
            }

            // Start streaming JSON to response
            Response.ContentType = "application/json; charset=utf-8";
            await using var writer = new Utf8JsonWriter(Response.Body, new JsonWriterOptions { SkipValidation = false });

            writer.WriteStartObject();
            writer.WriteString("status", "success");
            writer.WriteString("site_code", targetSiteCode);
            writer.WriteBoolean("compact", true);
            writer.WritePropertyName("points");
            writer.WriteStartArray();

            int totalPoints = 0;
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

            while (await reader.ReadAsync())
            {
                var lat = reader.IsDBNull(0) ? 0.0 : Convert.ToDouble(reader.GetValue(0));
                var lng = reader.IsDBNull(1) ? 0.0 : Convert.ToDouble(reader.GetValue(1));

                if (lat == 0.0 && lng == 0.0) continue;

                var gpsStatus = reader.IsDBNull(2) ? null : reader.GetValue(2)?.ToString();
                if (string.Equals(gpsStatus, "Not Fixed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(gpsStatus, "Error", StringComparison.OrdinalIgnoreCase))
                    continue;

                var devIdRaw = reader.IsDBNull(3) ? "" : reader.GetValue(3).ToString() ?? "";
                var devTime = reader.IsDBNull(4) ? (string?)null : Convert.ToDateTime(reader.GetValue(4)).ToString("yyyy-MM-ddTHH\\:mm\\:ss");
                double? rsrp = reader.IsDBNull(5) ? null : Convert.ToDouble(reader.GetValue(5));
                double? rssi = reader.IsDBNull(6) ? null : Convert.ToDouble(reader.GetValue(6));
                double? rsrq = reader.IsDBNull(7) ? null : Convert.ToDouble(reader.GetValue(7));
                double? sinr = reader.IsDBNull(8) ? null : Convert.ToDouble(reader.GetValue(8));

                var devName = deviceIdToName.TryGetValue(devIdRaw, out var dn) ? dn : devIdRaw;

                // Write compact array: [lat, lng, rsrp, rssi, rsrq, sinr, devName, devTime]
                writer.WriteStartArray();
                writer.WriteNumberValue(Math.Round(lat, 6));
                writer.WriteNumberValue(Math.Round(lng, 6));
                if (rsrp.HasValue) writer.WriteNumberValue(rsrp.Value); else writer.WriteNullValue();
                if (rssi.HasValue) writer.WriteNumberValue(rssi.Value); else writer.WriteNullValue();
                if (rsrq.HasValue) writer.WriteNumberValue(rsrq.Value); else writer.WriteNullValue();
                if (sinr.HasValue) writer.WriteNumberValue(sinr.Value); else writer.WriteNullValue();
                writer.WriteStringValue(devName);
                if (devTime != null) writer.WriteStringValue(devTime); else writer.WriteNullValue();
                writer.WriteEndArray();

                totalPoints++;

                // Flush to HTTP stream every 5000 rows to avoid buffering too much in memory
                if (totalPoints % 5000 == 0)
                {
                    await writer.FlushAsync();
                }
            }

            writer.WriteEndArray(); // end points[]
            writer.WriteNumber("total_points", totalPoints);
            writer.WriteEndObject();
            await writer.FlushAsync();

            return new EmptyResult(); // Response body already written via streaming
        }
        catch (Exception ex)
        {
            // If streaming hasn't started yet, we can still return a normal error JSON
            if (!Response.HasStarted)
            {
                return StatusCode(500, new { status = "error", message = "Failed to retrieve site track history: " + ex.Message });
            }
            Console.WriteLine($"[GetSiteTrackHistory Stream Error]: {ex.Message}");
            return new EmptyResult();
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetHourlySiteReport([FromQuery] string siteCode, [FromQuery] string? date)
    {
        try
        {
            if (string.IsNullOrEmpty(siteCode))
            {
                return BadRequest(new { status = "error", message = "siteCode parameter is required" });
            }

            var targetDate = !string.IsNullOrEmpty(date) ? date : DateTime.Now.ToString("yyyy-MM-dd");

            // Fetch active units belonging to this siteCode
            var parameters = new Dictionary<string, object?> { { "UserID", null }, { "OnlyActiveSites", 1 } };
            var rawSites = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetTrackingSitesWithUnits", parameters);

            var deviceIds = new List<string>();
            var deviceIdToName = new Dictionary<string, string>();

            foreach (var row in rawSites)
            {
                var rowDict = row as IDictionary<string, object>;
                if (rowDict != null)
                {
                    var sc = GetString(rowDict, "site_code");
                    if (string.Equals(sc, siteCode, StringComparison.OrdinalIgnoreCase))
                    {
                        var devId = GetString(rowDict, "device_id");
                        var devName = GetString(rowDict, "device_name", devId);
                        if (!string.IsNullOrEmpty(devId))
                        {
                            if (!deviceIds.Contains(devId)) deviceIds.Add(devId);
                            deviceIdToName[devId] = devName;
                        }
                    }
                }
            }

            if (!deviceIds.Any())
            {
                return Json(new { status = "success", site_code = siteCode, date = targetDate, kpi = new { }, hourly_data = new List<object>() });
            }

            // Execute track history retrieval with concurrency throttling (max 4 parallel DB queries at a time)
            using var semaphoreHourly = new SemaphoreSlim(4, 4);
            var tasks = deviceIds.Select(async devId =>
            {
                await semaphoreHourly.WaitAsync();
                try
                {
                    var p = new Dictionary<string, object?>
                    {
                        { "DeviceID", devId },
                        { "DateFrom", targetDate },
                        { "DateTo", targetDate },
                        { "TimeFrom", "00:00:00" },
                        { "TimeTo", "23:59:59" }
                    };
                    return await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetTrackingHistory", p);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GetHourlySiteReport Error for {devId}]: {ex.Message}");
                    return Enumerable.Empty<dynamic>();
                }
                finally
                {
                    semaphoreHourly.Release();
                }
            });

            var results = await Task.WhenAll(tasks);

            // Group points by Hour (0 to 23)
            var hourlyBuckets = new Dictionary<int, List<IDictionary<string, object>>>();
            for (int h = 0; h < 24; h++)
            {
                hourlyBuckets[h] = new List<IDictionary<string, object>>();
            }

            foreach (var trackPoints in results)
            {
                foreach (var point in trackPoints)
                {
                    var ptDict = point as IDictionary<string, object>;
                    if (ptDict == null) continue;

                    var lat = GetDouble(ptDict, "latitude");
                    var lng = GetDouble(ptDict, "longitude");
                    var gpsStatus = GetString(ptDict, "gps_status", "Unknown");

                    if ((lat == 0.0 && lng == 0.0) || string.Equals(gpsStatus, "Not Fixed", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var devTimeObj = GetDictValue(ptDict, "device_time");
                    if (devTimeObj != null && devTimeObj != DBNull.Value)
                    {
                        if (DateTime.TryParse(devTimeObj.ToString(), out DateTime dt))
                        {
                            hourlyBuckets[dt.Hour].Add(ptDict);
                        }
                    }
                }
            }

            var hourlyReportList = new List<object>();
            double totalRsrpSum = 0; int totalRsrpCount = 0;
            double totalRssiSum = 0; int totalRssiCount = 0;
            double totalRsrqSum = 0; int totalRsrqCount = 0;
            double totalSinrSum = 0; int totalSinrCount = 0;
            int peakUnits = 0;
            int lowCoverageHours = 0;

            for (int h = 0; h < 24; h++)
            {
                var bucket = hourlyBuckets[h];
                var hourLabel = $"{h:D2}:00 - {(h + 1):D2}:00";

                if (!bucket.Any())
                {
                    hourlyReportList.Add(new
                    {
                        hour = $"{h:D2}:00",
                        hour_label = hourLabel,
                        total_points = 0,
                        active_units = 0,
                        avg_rsrp = (double?)null,
                        avg_rssi = (double?)null,
                        avg_rsrq = (double?)null,
                        avg_sinr = (double?)null,
                        avg_speed = 0,
                        excellent = 0,
                        good = 0,
                        fair = 0,
                        poor = 0,
                        verypoor = 0
                    });
                    continue;
                }

                var activeUnitsCount = bucket.Select(p => GetString(p, "device_id")).Where(id => !string.IsNullOrEmpty(id)).Distinct().Count();
                if (activeUnitsCount > peakUnits) peakUnits = activeUnitsCount;

                var rsrpVals = bucket.Select(p => GetNullableDouble(p, "network_rsrp")).Where(v => v.HasValue).Select(v => v!.Value).ToList();
                var rssiVals = bucket.Select(p => GetNullableDouble(p, "network_rssi")).Where(v => v.HasValue).Select(v => v!.Value).ToList();
                var rsrqVals = bucket.Select(p => GetNullableDouble(p, "network_rsrq")).Where(v => v.HasValue).Select(v => v!.Value).ToList();
                var sinrVals = bucket.Select(p => GetNullableDouble(p, "network_sinr")).Where(v => v.HasValue).Select(v => v!.Value).ToList();
                var speedVals = bucket.Select(p => (double)GetInt(p, "speed_kmh")).ToList();

                double? avgRsrp = rsrpVals.Any() ? Math.Round(rsrpVals.Average(), 1) : null;
                double? avgRssi = rssiVals.Any() ? Math.Round(rssiVals.Average(), 1) : null;
                double? avgRsrq = rsrqVals.Any() ? Math.Round(rsrqVals.Average(), 1) : null;
                double? avgSinr = sinrVals.Any() ? Math.Round(sinrVals.Average(), 1) : null;
                double avgSpeed = speedVals.Any() ? Math.Round(speedVals.Average(), 1) : 0;

                if (avgRsrp.HasValue)
                {
                    totalRsrpSum += avgRsrp.Value;
                    totalRsrpCount++;
                    if (avgRsrp.Value < -105.0) lowCoverageHours++;
                }
                if (avgRssi.HasValue) { totalRssiSum += avgRssi.Value; totalRssiCount++; }
                if (avgRsrq.HasValue) { totalRsrqSum += avgRsrq.Value; totalRsrqCount++; }
                if (avgSinr.HasValue) { totalSinrSum += avgSinr.Value; totalSinrCount++; }

                // Count signal quality distribution levels
                int exc = 0, gdd = 0, fir = 0, prr = 0, vpr = 0;
                foreach (var v in rsrpVals)
                {
                    if (v >= -85) exc++;
                    else if (v >= -100) gdd++;
                    else if (v >= -105) fir++;
                    else if (v >= -115) prr++;
                    else vpr++;
                }

                hourlyReportList.Add(new
                {
                    hour = $"{h:D2}:00",
                    hour_label = hourLabel,
                    total_points = bucket.Count,
                    active_units = activeUnitsCount,
                    avg_rsrp = avgRsrp,
                    avg_rssi = avgRssi,
                    avg_rsrq = avgRsrq,
                    avg_sinr = avgSinr,
                    avg_speed = avgSpeed,
                    excellent = exc,
                    good = gdd,
                    fair = fir,
                    poor = prr,
                    verypoor = vpr
                });
            }

            var kpi = new
            {
                avg_rsrp = totalRsrpCount > 0 ? Math.Round(totalRsrpSum / totalRsrpCount, 1) : (double?)null,
                avg_rssi = totalRssiCount > 0 ? Math.Round(totalRssiSum / totalRssiCount, 1) : (double?)null,
                avg_rsrq = totalRsrqCount > 0 ? Math.Round(totalRsrqSum / totalRsrqCount, 1) : (double?)null,
                avg_sinr = totalSinrCount > 0 ? Math.Round(totalSinrSum / totalSinrCount, 1) : (double?)null,
                peak_units = peakUnits,
                low_coverage_hours = lowCoverageHours,
                total_devices = deviceIds.Count
            };

            return Json(new { status = "success", site_code = siteCode, date = targetDate, kpi = kpi, hourly_data = hourlyReportList });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = "Failed to retrieve hourly site report: " + ex.Message });
        }
    }

    [HttpGet("{deviceId}")]
    public async Task<IActionResult> GetCurrentPosition(string deviceId)
    {
        try
        {
            var parameters = new Dictionary<string, object?> { { "DeviceID", deviceId } };
            var positions = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetCurrentTrackingPosition", parameters);

            if (!positions.Any())
                return NotFound(new { status = "error", message = "Device not found or no recent position data" });

            var position = positions.First();
            
            return Json(new
            {
                status = "success",
                message = "Current position retrieved",
                data = new
                {
                    device_id = position.device_id,
                    device_name = position.device_name,
                    serial_number = position.serial_number,
                    fleet_type = position.fleet_type ?? "Heavy Duty Truck",
                    site_code = position.site_code,
                    latitude = Convert.ToDouble(position.latitude ?? 0),
                    longitude = Convert.ToDouble(position.longitude ?? 0),
                    speed_kmh = Convert.ToInt32(position.speed_kmh ?? 0),
                    heading_degree = Convert.ToInt32(position.heading_degree ?? 0),
                    altitude = Convert.ToDouble(position.altitude ?? 0),
                    gps_status = position.gps_status,
                    satellite_in_use = Convert.ToInt32(position.satellite_in_use ?? 0),
                    status = position.status,
                    timestamp = position.device_time != null ? Convert.ToDateTime(position.device_time).ToString("s") : "-",
                    network_quality = new
                    {
                        rssi = position.network_rssi != null ? (double?)Convert.ToDouble(position.network_rssi) : null,
                        rsrp = position.network_rsrp != null ? (double?)Convert.ToDouble(position.network_rsrp) : null,
                        rsrq = position.network_rsrq != null ? (double?)Convert.ToDouble(position.network_rsrq) : null,
                        sinr = position.network_sinr != null ? (double?)Convert.ToDouble(position.network_sinr) : null
                    }
                },
                meta = new
                {
                    retrieved_at = DateTime.UtcNow.ToString("s")
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = "Failed to retrieve current position: " + ex.Message });
        }
    }

    [HttpGet("{deviceId}")]
    public async Task<IActionResult> GetUnitDetail(string deviceId)
    {
        try
        {
            var parameters = new Dictionary<string, object?> { { "DeviceID", deviceId } };
            var positions = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetCurrentTrackingPosition", parameters);

            if (!positions.Any())
                return NotFound(new { status = "error", message = "Device not found" });

            var device = positions.First();
            
            return Json(new
            {
                status = "success",
                message = "Unit detail retrieved",
                data = new
                {
                    device_id = device.device_id,
                    serial_number = device.serial_number,
                    device_name = device.device_name,
                    fleet_type = device.fleet_type ?? "Heavy Duty Truck",
                    site_code = device.site_code,
                    site_name = device.site_name,
                    current_location = new
                    {
                        latitude = Convert.ToDouble(device.latitude ?? 0),
                        longitude = Convert.ToDouble(device.longitude ?? 0),
                        heading_degree = Convert.ToInt32(device.heading_degree ?? 0),
                        gps_status = device.gps_status,
                        satellite_in_use = Convert.ToInt32(device.satellite_in_use ?? 0),
                        altitude = Convert.ToDouble(device.altitude ?? 0)
                    },
                    movement = new
                    {
                        speed_kmh = Convert.ToInt32(device.speed_kmh ?? 0),
                        status = device.status,
                        last_update = device.device_time != null ? Convert.ToDateTime(device.device_time).ToString("s") : "-"
                    },
                    network_quality = new
                    {
                        rssi = device.network_rssi != null ? (double?)Convert.ToDouble(device.network_rssi) : null,
                        rsrp = device.network_rsrp != null ? (double?)Convert.ToDouble(device.network_rsrp) : null,
                        rsrq = device.network_rsrq != null ? (double?)Convert.ToDouble(device.network_rsrq) : null,
                        sinr = device.network_sinr != null ? (double?)Convert.ToDouble(device.network_sinr) : null
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = "Failed to retrieve unit detail: " + ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetDailyStatistics([FromQuery] string? site_id, [FromQuery] string date_from, [FromQuery] string date_to)
    {
        try
        {
            if (string.IsNullOrEmpty(date_from)) date_from = DateTime.Now.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(date_to)) date_to = DateTime.Now.ToString("yyyy-MM-dd");

            var parameters = new Dictionary<string, object?> 
            { 
                { "SiteID", site_id },
                { "DateFrom", date_from },
                { "DateTo", date_to }
            };
            
            var stats = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetTrackingDailyStatistics", parameters);
            
            var groupedStats = new Dictionary<string, List<object>>();
            
            foreach(var row in stats)
            {
                var dateKey = row.track_date != null ? Convert.ToDateTime(row.track_date).ToString("yyyy-MM-dd") : "-";
                if (!groupedStats.ContainsKey(dateKey))
                    groupedStats[dateKey] = new List<object>();
                    
                groupedStats[dateKey].Add(new
                {
                    site_code = row.site_code,
                    device_id = row.device_id,
                    device_name = row.device_name,
                    serial_number = row.serial_number,
                    total_points = Convert.ToInt32(row.total_points ?? 0),
                    total_distance_km = Convert.ToDouble(row.total_distance_km ?? 0),
                    total_stops = Convert.ToInt32(row.total_stops ?? 0),
                    idle_time_minutes = Math.Round(Convert.ToDouble(row.idle_time_seconds ?? 0) / 60.0, 2),
                    avg_speed_kmh = Convert.ToDouble(row.avg_speed_kmh ?? 0),
                    max_speed_kmh = Convert.ToDouble(row.max_speed_kmh ?? 0)
                });
            }

            return Json(new
            {
                status = "success",
                message = "Daily statistics retrieved",
                meta = new
                {
                    date_from,
                    date_to,
                    site_id,
                    total_records = stats.Count(),
                    timestamp = DateTime.UtcNow.ToString("s")
                },
                data = groupedStats
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = "Failed to retrieve statistics: " + ex.Message });
        }
    }
}
