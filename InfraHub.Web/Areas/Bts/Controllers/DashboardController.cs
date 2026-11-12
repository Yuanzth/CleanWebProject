using InfraHub.Core.Interfaces;
using InfraHub.Core.Helpers;
using InfraHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InfraHub.Web.Areas.Bts.Controllers;

[Area("Bts")]
[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _context;
    private readonly IStoredProcedureExecutor _spExecutor;

    public DashboardController(AppDbContext context, IStoredProcedureExecutor spExecutor)
    {
        _context = context;
        _spExecutor = spExecutor;
    }

    private async Task<List<string>?> GetAuthorizedSiteIdsAsync()
    {
        var roleName = User.FindFirst("RoleName")?.Value;
        if (RoleHelper.IsAdmin(roleName))
        {
            return null; // null means all sites (Administrator & NOC)
        }

        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return new List<string>();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return new List<string>();

        var parameters = new Dictionary<string, object?> { { "UserId", user.Id } };
        
        // sp_IsUserAdministrator (safe check)
        bool isAdmin = false;
        try
        {
            var adminResult = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_IsUserAdministrator", parameters);
            var firstRow = adminResult.FirstOrDefault() as IDictionary<string, object>;
            if (firstRow != null)
            {
                var key = firstRow.Keys.FirstOrDefault(k => string.Equals(k, "is_admin", StringComparison.OrdinalIgnoreCase));
                if (key != null && firstRow[key] != null)
                {
                    isAdmin = Convert.ToBoolean(firstRow[key]);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dashboard] Error checking sp_IsUserAdministrator: {ex.Message}");
        }

        Console.WriteLine($"IsAdmin Evaluated to: {isAdmin} for user {user.Username}");

        if (isAdmin)
        {
            return null; // null means all sites
        }

        var siteIds = new List<string>();
        try
        {
            var assignedResult = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetUserAssignedSites", parameters);
            foreach (var row in assignedResult)
            {
                var dict = (IDictionary<string, object>)row;
                var siteKey = dict.Keys.FirstOrDefault(k => string.Equals(k, "site_id", StringComparison.OrdinalIgnoreCase));
                if (siteKey != null && dict[siteKey] != null)
                    siteIds.Add(dict[siteKey].ToString()!);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dashboard] Error calling sp_GetUserAssignedSites: {ex.Message}");
        }

        return siteIds;
    }

    public async Task<IActionResult> Index()
    {
        var authorizedSiteIds = await GetAuthorizedSiteIdsAsync();
        var isAdmin = authorizedSiteIds == null;

        var districtRows = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_ReportGetSiteFilters");

        if (!isAdmin)
        {
            var allowed = new HashSet<string>(authorizedSiteIds.Select(x => x.ToLower()));
            districtRows = districtRows.Where(r => r.id != null && allowed.Contains(r.id.ToString().ToLower())).ToList();
        }

        string? defaultSiteId = null;
        if (!isAdmin && authorizedSiteIds != null && authorizedSiteIds.Any())
        {
            defaultSiteId = authorizedSiteIds.First();
        }

        var viewData = await BuildSummaryDataAsync(defaultSiteId);

        // Map nested structure into ViewBag for server-side rendering
        ViewBag.stats = viewData.ContainsKey("stats") ? viewData["stats"] : null;
        ViewBag.generator = viewData.ContainsKey("generator") ? viewData["generator"] : null;
        ViewBag.tower = viewData.ContainsKey("tower") ? viewData["tower"] : null;
        ViewBag.environmental = viewData.ContainsKey("environmental") ? viewData["environmental"] : null;
        ViewBag.towerDownItems = viewData.ContainsKey("towerDownItems") ? viewData["towerDownItems"] : new List<object>();
        ViewBag.generatorDownItems = viewData.ContainsKey("generatorDownItems") ? viewData["generatorDownItems"] : new List<object>();
        ViewBag.btsDownItems = viewData.ContainsKey("btsDownItems") ? viewData["btsDownItems"] : new List<object>();

        ViewBag.Districts = districtRows;
        ViewBag.Selected = defaultSiteId ?? "all";
        ViewBag.IsAdmin = isAdmin;
        ViewBag.AuthorizedSites = authorizedSiteIds ?? new List<string>();
        ViewBag.is_admin = isAdmin;

        return View();
    }

    [HttpGet("/api/dashboard/summary")]
    public async Task<IActionResult> GetDashboardSummary(string? site_id)
    {
        var authorizedSiteIds = await GetAuthorizedSiteIdsAsync();
        
        var isAll = string.IsNullOrEmpty(site_id) || site_id == "all";
        var paramId = isAll ? null : site_id;

        if (authorizedSiteIds != null)
        {
            if (isAll)
            {
                return StatusCode(403, new { status = "error", message = "Access denied. You can only view assigned sites." });
            }
            if (!authorizedSiteIds.Contains(paramId))
            {
                return StatusCode(403, new { status = "error", message = "Access denied. This site is not assigned to you." });
            }
        }

        try
        {
            var nested = await BuildSummaryDataAsync(paramId);

            return Json(new {
                status = "success",
                data = new {
                    stats = nested["stats"],
                    generator = nested["generator"],
                    tower = nested["tower"],
                    environmental = nested["environmental"],
                    towerDownItems = nested["towerDownItems"],
                    generatorDownItems = nested["generatorDownItems"],
                    btsDownItems = nested["btsDownItems"]
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dashboard] GetDashboardSummary error: {ex}");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Helper: safely read a value from an ExpandoObject dictionary
    // ──────────────────────────────────────────────────────────────
    private static object? Get(IDictionary<string, object>? d, string key)
    {
        if (d == null) return null;
        return d.TryGetValue(key, out var v) ? v : null;
    }

    private static int GetInt(IDictionary<string, object>? d, string key, int fallback = 0)
    {
        var v = Get(d, key);
        if (v == null) return fallback;
        try { return Convert.ToInt32(v); } catch { return fallback; }
    }

    private static double GetDouble(IDictionary<string, object>? d, string key, double fallback = 0)
    {
        var v = Get(d, key);
        if (v == null) return fallback;
        try { return Convert.ToDouble(v); } catch { return fallback; }
    }

    private static string GetStr(IDictionary<string, object>? d, string key, string fallback = "")
    {
        return GetAnyStr(d, new[] { key }, fallback);
    }

    private static string GetAnyStr(IDictionary<string, object>? d, string[] keys, string fallback = "-")
    {
        if (d == null) return fallback;
        foreach (var key in keys)
        {
            if (d.TryGetValue(key, out var v) && v != null && !string.IsNullOrWhiteSpace(v.ToString()))
            {
                return v.ToString()!;
            }
        }
        // Try case-insensitive key search
        foreach (var key in keys)
        {
            var match = d.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (match != null && d[match] != null && !string.IsNullOrWhiteSpace(d[match]!.ToString()))
            {
                return d[match]!.ToString()!;
            }
        }
        return fallback;
    }

    // ──────────────────────────────────────────────────────────────
    // BUILD SUMMARY — integrates real SP data
    // ──────────────────────────────────────────────────────────────
    private async Task<Dictionary<string, object>> BuildSummaryDataAsync(string? paramId)
    {
        var parameters = new Dictionary<string, object?> { { "SiteID", paramId } };

        // 1. Master Summary (stat cards)
        IDictionary<string, object>? master = null;
        try
        {
            var masterRows = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetDashboardMasterSummary", parameters);
            var firstRow = masterRows.FirstOrDefault();
            if (firstRow != null) master = (IDictionary<string, object>)firstRow;
        }
        catch (Exception ex) { Console.WriteLine($"[Dashboard] sp_GetDashboardMasterSummary error: {ex.Message}"); }

        // 2. Fallback realtime metrics (avg wind, temp, tilt, fuel, etc.)
        IDictionary<string, object>? fallback = null;
        try
        {
            var fbRows = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetDashboardFallbackMetrics", parameters);
            var fbFirst = fbRows.FirstOrDefault();
            if (fbFirst != null) fallback = (IDictionary<string, object>)fbFirst;
        }
        catch (Exception ex) { Console.WriteLine($"[Dashboard] sp_GetDashboardFallbackMetrics error: {ex.Message}"); }

        // 3. Anomaly cards
        var anomalyList = new List<IDictionary<string, object>>();
        try
        {
            var anomalyRows = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetDashboardAnomalies", parameters);
            foreach (var a in anomalyRows)
            {
                anomalyList.Add((IDictionary<string, object>)a);
            }
        }
        catch (Exception ex) { Console.WriteLine($"[Dashboard] sp_GetDashboardAnomalies error: {ex.Message}"); }

        // 4. Down device lists
        var towerDownItems = new List<object>();
        var generatorDownItems = new List<object>();
        var btsDownItems = new List<object>();
        try
        {
            var downRows = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetDownDeviceList", parameters);
            foreach (var d in downRows)
            {
                var dict = (IDictionary<string, object>)d;
                var category = GetAnyStr(dict, new[] { "category", "type", "device_type" }, "tower").ToLower();
                var nameVal = GetAnyStr(dict, new[] { "device_name", "name", "device_code", "zone_name", "zone_code", "title" }, "-");
                var siteVal = GetAnyStr(dict, new[] { "site_name", "site", "site_code", "district_name" }, "-");
                var zoneVal = GetAnyStr(dict, new[] { "zone_id", "id", "area_id" }, "");
                var statusVal = GetAnyStr(dict, new[] { "severity", "status", "level" }, "warning");
                var lastSeenVal = GetAnyStr(dict, new[] { "last_seen", "device_time", "created_at", "updated_at", "time" }, "");

                var item = new {
                    name = nameVal,
                    site = siteVal,
                    zone_id = zoneVal,
                    status = statusVal,
                    last_seen = lastSeenVal
                };

                switch (category)
                {
                    case "tower": towerDownItems.Add(item); break;
                    case "generator": generatorDownItems.Add(item); break;
                    case "bts": btsDownItems.Add(item); break;
                    default: towerDownItems.Add(item); break;
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[Dashboard] sp_GetDownDeviceList error: {ex.Message}"); }

        // ── Build stat cards ──
        int towerUp = GetInt(master, "tower_up");
        int towerDown = GetInt(master, "tower_down", towerDownItems.Count);
        int towerTotal = GetInt(master, "tower_total", towerUp + towerDown);
        int generatorDown = GetInt(master, "generator_down", generatorDownItems.Count);
        int btsTowerDown = GetInt(master, "bts_tower_down", btsDownItems.Count);

        // ── Build generator panel ──
        int genTotal = GetInt(master, "generator_total", GetInt(fallback, "generator_total"));
        int genReady = GetInt(master, "generator_ready", GetInt(fallback, "generator_ready"));
        double avgFuel = GetDouble(fallback, "avg_fuel", GetDouble(master, "avg_fuel", 0));
        int genReadyPercent = genTotal > 0 ? (int)Math.Round((double)genReady / genTotal * 100) : 0;
        string electricityStatus = GetStr(fallback, "electricity_status", GetStr(master, "electricity_status", "Normal"));
        string lightningStatus = GetStr(fallback, "lightning_status", GetStr(master, "lightning_status", "Normal"));

        // ── Build tower panel ──
        double avgTilt = GetDouble(fallback, "avg_tilt", GetDouble(master, "avg_tilt", 0));
        string tiltStatus = avgTilt >= 1.0 ? "Danger" : (avgTilt >= 0.5 ? "Warning" : "Safe");
        string spdHealth = GetStr(fallback, "spd_health", GetStr(master, "spd_health", "Good"));
        string earthResistance = GetStr(fallback, "earth_resistance_status", GetStr(master, "earth_resistance_status", "Normal"));
        string lightingActivity = GetStr(fallback, "lighting_activity", GetStr(master, "lighting_activity", "Normal"));

        // ── Build environmental panel ──
        double avgWind = GetDouble(fallback, "avg_wind_speed", GetDouble(master, "avg_wind", 0));
        double avgTemp = GetDouble(fallback, "avg_temperature", GetDouble(master, "avg_temp", 0));
        string dustStatus = GetStr(fallback, "dust_status", GetStr(master, "dust_status", "Normal"));
        string avgDust = GetStr(fallback, "avg_dust", GetStr(master, "avg_dust", "Good"));

        // ── Categorize anomalies by panel ──
        var generatorAnomalies = new List<object>();
        var towerAnomalies = new List<object>();
        var environmentalAnomalies = new List<object>();

        foreach (var anm in anomalyList)
        {
            var panel = GetAnyStr(anm, new[] { "panel", "category" }, "tower").ToLower();
            var anomalyObj = new {
                anomaly_id = GetAnyStr(anm, new[] { "anomaly_id", "id" }, ""),
                zone_id = GetAnyStr(anm, new[] { "zone_id", "area_id" }, ""),
                site = GetAnyStr(anm, new[] { "site_name", "site", "site_code" }, "-"),
                name = GetAnyStr(anm, new[] { "device_name", "name", "device_code" }, "-"),
                type = GetAnyStr(anm, new[] { "type", "anomaly_type" }, "warning"),
                status = GetAnyStr(anm, new[] { "severity", "status" }, "warning"),
                value = Get(anm, "value"),
                panel = panel,
                acknowledged = Get(anm, "is_acknowledged") ?? Get(anm, "acknowledged") ?? false,
                meta = new {
                    voltage = Get(anm, "voltage"),
                    frequency = Get(anm, "frequency"),
                    voltage_status = GetStr(anm, "voltage_status"),
                    freq_status = GetStr(anm, "freq_status")
                }
            };

            switch (panel)
            {
                case "generator": generatorAnomalies.Add(anomalyObj); break;
                case "tower": towerAnomalies.Add(anomalyObj); break;
                case "environmental": environmentalAnomalies.Add(anomalyObj); break;
                default: towerAnomalies.Add(anomalyObj); break;
            }
        }

        // Determine panel statuses & all_normal
        bool hasTowerDown = towerDownItems.Any();
        bool hasGenDown = generatorDownItems.Any();
        bool hasBtsDown = btsDownItems.Any();

        bool genNormal = !generatorAnomalies.Any() && !hasGenDown;
        bool towerNormal = !towerAnomalies.Any() && !hasTowerDown && !hasBtsDown;
        bool envNormal = !environmentalAnomalies.Any();

        string genStatus = genNormal ? "normal" : "warning";
        string towerPanelStatus = towerNormal ? "normal" : "warning";
        string envStatus = envNormal ? "normal" : "warning";

        return new Dictionary<string, object>
        {
            { "stats", new {
                tower_up = towerUp, tower_down = towerDown, tower_total = towerTotal,
                generator_down = generatorDown, bts_tower_down = btsTowerDown
            }},
            { "generator", new {
                total = genTotal, ready_count = genReady, ready_percent = genReadyPercent,
                avg_fuel = avgFuel, electricity = electricityStatus,
                lightning_status = lightningStatus,
                status = genStatus,
                anomalies = generatorAnomalies,
                all_normal = genNormal
            }},
            { "tower", new {
                avg_tilt = Math.Round(avgTilt, 2),
                tilt_status = tiltStatus,
                spd_health = spdHealth,
                earth_resistance = earthResistance,
                lighting_activity = lightingActivity,
                status = towerPanelStatus,
                anomalies = towerAnomalies,
                all_normal = towerNormal
            }},
            { "environmental", new {
                avg_wind = Math.Round(avgWind, 1),
                avg_temp = Math.Round(avgTemp, 1),
                dust_status = dustStatus,
                avg_dust = avgDust,
                wind_status = avgWind > 80 ? "Danger" : (avgWind > 35 ? "Warning" : "Normal"),
                status = envStatus,
                anomalies = environmentalAnomalies,
                all_normal = envNormal
            }},
            { "towerDownItems", towerDownItems },
            { "generatorDownItems", generatorDownItems },
            { "btsDownItems", btsDownItems }
        };
    }
}

