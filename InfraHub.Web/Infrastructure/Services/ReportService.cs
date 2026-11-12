using System.Data;
using InfraHub.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace InfraHub.Infrastructure.Services;

public class ReportService
{
    private readonly AppDbContext _context;

    public ReportService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<dynamic> GetReportSummaryAsync(Guid? areaId, DateTime fromDate, DateTime toDate)
    {
        var connection = _context.Database.GetDbConnection();

        // ── 1. Summary Aggregate ──
        var p = new DynamicParameters();
        p.Add("@SiteID", areaId);
        p.Add("@DateFrom", fromDate.ToString("yyyy-MM-dd"));
        p.Add("@DateTo", toDate.ToString("yyyy-MM-dd"));
        p.Add("@VoltageMin", 215.0);
        p.Add("@VoltageMax", 225.0);
        p.Add("@FrequencyMin", 49.5);
        p.Add("@FrequencyMax", 50.5);
        p.Add("@PFMin", 0.85);
        p.Add("@FuelCritical", 20.0);
        p.Add("@FuelWarning", 35.0);
        p.Add("@WindThreshold", 50.0);
        p.Add("@WindCritical", 80.0);
        p.Add("@RainThreshold", 50.0);
        p.Add("@TiltWarn", 0.8);
        p.Add("@TiltLimit", 2.0);
        p.Add("@LightningLimit", 20.0);

        dynamic? summary = null;
        try
        {
            summary = await connection.QueryFirstOrDefaultAsync(
                "sp_ReportSummaryAggregate", 
                p, 
                commandType: CommandType.StoredProcedure);
        }
        catch (Exception ex) { Console.WriteLine($"[ReportService] sp_ReportSummaryAggregate error: {ex.Message}"); }

        // ── 2. Tower Attention ──
        var attentionTowers = new List<object>();
        try
        {
            var pAtt = new DynamicParameters();
            pAtt.Add("@SiteID", areaId);
            pAtt.Add("@DateFrom", fromDate.ToString("yyyy-MM-dd"));
            pAtt.Add("@DateTo", toDate.ToString("yyyy-MM-dd"));
            pAtt.Add("@TiltWarn", 0.8);
            pAtt.Add("@TopN", 5);
            var attentionTowersRaw = await connection.QueryAsync(
                "sp_ReportAttentionTowers",
                pAtt,
                commandType: CommandType.StoredProcedure);

            attentionTowers = attentionTowersRaw.Select(r => new {
                site_code = (string)(r.site_code ?? ""),
                tilt = Math.Round((double)(r.max_tilt ?? 0), 1),
                ground_r = Math.Round((double)(r.avg_ground ?? 0), 1),
                spd = (int)(r.spd ?? 0),
                severity = (double)(r.max_tilt ?? 0) > 2.0 ? "crit" : "warn",
                note = (double)(r.max_tilt ?? 0) > 2.0 ? "Tilt melewati batas operasional — inspeksi segera" : "Perlu monitoring struktur dalam periode dekat"
            }).Cast<object>().ToList();
        }
        catch (Exception ex) { Console.WriteLine($"[ReportService] sp_ReportAttentionTowers error: {ex.Message}"); }

        // ── 3. Recent Genset ──
        IEnumerable<dynamic> recentGenset = new List<dynamic>();
        try
        {
            var pGenset = new DynamicParameters();
            pGenset.Add("@SiteID", areaId);
            pGenset.Add("@DateFrom", fromDate.ToString("yyyy-MM-dd"));
            pGenset.Add("@DateTo", toDate.ToString("yyyy-MM-dd"));
            pGenset.Add("@TopN", 10);
            recentGenset = await connection.QueryAsync("sp_ReportRecentGenset", pGenset, commandType: CommandType.StoredProcedure);
        }
        catch (Exception ex) { Console.WriteLine($"[ReportService] sp_ReportRecentGenset error: {ex.Message}"); }

        // ── 4. Trend Per Device (for charts) ──
        object? trendsPerDevice = null;
        object? towerHeights = null;
        try
        {
            var pTrend = new DynamicParameters();
            pTrend.Add("@SiteID", areaId);
            pTrend.Add("@DateFrom", fromDate.ToString("yyyy-MM-dd"));
            pTrend.Add("@DateTo", toDate.ToString("yyyy-MM-dd"));
            var trendRows = await connection.QueryAsync("sp_ReportTrendPerDevice", pTrend, commandType: CommandType.StoredProcedure);

            trendsPerDevice = BuildTrendData(trendRows);
            towerHeights = BuildTowerHeights(trendRows);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReportService] sp_ReportTrendPerDevice error: {ex.Message}");
            trendsPerDevice = new {};
            towerHeights = new {};
        }

        // ── Build the nested structure ──
        double avgVoltage = summary?.avg_voltage != null ? (double)summary.avg_voltage : 0;
        double avgPowerFactor = summary?.avg_power_factor != null ? (double)summary.avg_power_factor : 0;
        
        string powerStatus = (avgVoltage >= 215.0 && avgVoltage <= 225.0 && avgPowerFactor >= 0.85) ? "ok" : "warn";
        
        double maxWind = summary?.max_wind_speed != null ? (double)summary.max_wind_speed : 0;
        double maxRain = summary?.max_rainfall != null ? (double)summary.max_rainfall : 0;
        int totalLightning = summary?.total_lightning != null ? (int)summary.total_lightning : 0;
        string weatherStatus = (maxWind >= 50.0 || maxRain > 50.0 || totalLightning > 0) ? "warn" : "ok";
        
        double maxTilt = summary?.max_tilt != null ? (double)summary.max_tilt : 0;
        double minTilt = summary?.min_tilt != null ? (double)summary.min_tilt : 0;
        string towerStatus = maxTilt > 2.0 ? "crit" : (maxTilt > 0.8 ? "warn" : "ok");

        int totalSites = summary?.total_sites != null ? (int)summary.total_sites : 0;
        int criticalSites = summary?.critical_sites != null ? (int)summary.critical_sites : 0;
        int nominalSites = summary?.nominal_sites != null ? (int)summary.nominal_sites : 0;
        int overallHealth = totalSites > 0 ? (int)Math.Round((nominalSites / (double)totalSites) * 100) : 100;

        // Incident counts per category
        int fuelCritical = SafeInt(summary?.fuel_critical);
        int fuelWarning = SafeInt(summary?.fuel_warning);
        int plnCritical = SafeInt(summary?.pln_critical);
        int plnWarning = SafeInt(summary?.pln_warning);
        int windCritical = SafeInt(summary?.wind_critical);
        int windWarning = SafeInt(summary?.wind_warning);
        int rainCritical = SafeInt(summary?.rain_critical);
        int rainWarning = SafeInt(summary?.rain_warning);
        int lightningCritical = SafeInt(summary?.lightning_critical);
        int lightningWarning = SafeInt(summary?.lightning_warning);
        int tiltCritical = SafeInt(summary?.tilt_critical);
        int tiltWarning = SafeInt(summary?.tilt_warning);

        int totalCritical = fuelCritical + plnCritical + windCritical + rainCritical + lightningCritical + tiltCritical;
        int totalWarning = fuelWarning + plnWarning + windWarning + rainWarning + lightningWarning + tiltWarning;
        int totalIncidents = totalCritical + totalWarning;

        // Build by_category for incident chart
        var byCategory = new List<object>();
        AddCategory(byCategory, "Fuel Level", fuelCritical, fuelWarning);
        AddCategory(byCategory, "PLN / Voltage", plnCritical, plnWarning);
        AddCategory(byCategory, "Wind Speed", windCritical, windWarning);
        AddCategory(byCategory, "Rainfall", rainCritical, rainWarning);
        AddCategory(byCategory, "Lightning", lightningCritical, lightningWarning);
        AddCategory(byCategory, "Tower Tilt", tiltCritical, tiltWarning);

        double minGround = summary?.min_ground_resistance != null ? Math.Round((double)summary.min_ground_resistance, 1) : 0;
        double maxGround = summary?.max_ground_resistance != null ? Math.Round((double)summary.max_ground_resistance, 1) : 0;
        double minVoltage = summary?.min_voltage != null ? Math.Round((double)summary.min_voltage, 1) : 0;
        double maxVoltageVal = summary?.max_voltage != null ? Math.Round((double)summary.max_voltage, 1) : 0;

        return new
        {
            meta = new
            {
                generated_at = DateTime.Now.ToString("o"),
                is_dummy = false,
                period_label = $"{fromDate:dd MMM} - {toDate:dd MMM yyyy}",
                total_sites = totalSites,
                total_devices = summary?.total_devices ?? 0
            },
            kpi = new
            {
                overall_health = new
                {
                    value = overallHealth,
                    sub = $"{nominalSites} / {totalSites} sites ok",
                    status = overallHealth >= 95 ? "ok" : (overallHealth >= 85 ? "warn" : "crit"),
                    badge = overallHealth >= 95 ? "GOOD" : (overallHealth >= 85 ? "MONITOR" : "ACTION")
                },
                power = new
                {
                    value = Math.Round(avgVoltage).ToString(),
                    sub = $"{summary?.anomaly_count ?? 0} anomalies detected",
                    status = powerStatus,
                    badge = powerStatus == "ok" ? "NORMAL" : "MONITOR"
                },
                weather = new
                {
                    value = Math.Round((double)(summary?.avg_wind_speed ?? 0)).ToString(),
                    sub = $"Max {Math.Round(maxWind, 1)} km/h · Rain {Math.Round((double)(summary?.avg_rainfall ?? 0), 1)} mm/h",
                    status = weatherStatus,
                    badge = weatherStatus == "ok" ? "NORMAL" : "MONITOR"
                },
                tower = new
                {
                    value = Math.Round((double)(summary?.avg_tilt ?? 0), 1).ToString("0.0"),
                    sub = $"Max tilt {Math.Round(maxTilt, 1)}°",
                    status = towerStatus,
                    badge = towerStatus == "ok" ? "NORMAL" : (towerStatus == "warn" ? "MONITOR" : "ACTION")
                },
                incidents = new
                {
                    value = totalIncidents,
                    sub = $"{totalCritical} critical · {totalWarning} warning",
                    status = totalCritical > 0 ? "crit" : (totalWarning > 0 ? "warn" : "ok"),
                    badge = totalCritical > 0 ? "ACTION" : (totalWarning > 0 ? "MONITOR" : "GOOD")
                }
            },
            power = new
            {
                avg_voltage = Math.Round(avgVoltage, 1),
                avg_current = Math.Round((double)(summary?.avg_current ?? 0), 1),
                avg_power_kw = Math.Round((double)(summary?.avg_power_kw ?? 0), 1),
                avg_frequency = Math.Round((double)(summary?.avg_frequency ?? 0), 2),
                avg_power_factor = Math.Round(avgPowerFactor, 2),
                total_energy_kwh = Math.Round((double)(summary?.total_energy_kwh ?? 0), 1),
                voltage_ok = avgVoltage >= 215.0 && avgVoltage <= 225.0,
                frequency_ok = (double)(summary?.avg_frequency ?? 50) >= 49.5 && (double)(summary?.avg_frequency ?? 50) <= 50.5,
                pf_ok = avgPowerFactor >= 0.85,
                anomaly_count = summary?.anomaly_count ?? 0,
                min_voltage = minVoltage,
                max_voltage = maxVoltageVal,
                trend_upper_limit = 225.0,
                trend_lower_limit = 215.0
            },
            incidents = new
            {
                total = totalIncidents,
                total_critical = totalCritical,
                total_warning = totalWarning,
                by_category = byCategory,
                recent = new List<dynamic>()
            },
            tower = new
            {
                avg_tilt = Math.Round((double)(summary?.avg_tilt ?? 0), 1),
                min_tilt = Math.Round(minTilt, 1),
                max_tilt = Math.Round(maxTilt, 1),
                tilt_limit = 2.0,
                avg_ground_resistance = Math.Round((double)(summary?.avg_ground_resistance ?? 0), 1),
                min_ground_resistance = minGround,
                max_ground_resistance = maxGround,
                total_spd_events = summary?.total_spd_events ?? 0,
                max_lightning_current = Math.Round((double)(summary?.max_lightning_current ?? 0), 1),
                lightning_limit_ka = 20.0,
                attention_towers = attentionTowers,
                tower_heights = towerHeights
            },
            weather = new
            {
                avg_wind_speed = Math.Round((double)(summary?.avg_wind_speed ?? 0), 1),
                max_wind_speed = maxWind,
                min_wind_speed = summary?.min_wind_speed != null ? Math.Round((double)summary.min_wind_speed, 1) : 0,
                wind_ok = maxWind < 50.0,
                avg_rainfall = Math.Round((double)(summary?.avg_rainfall ?? 0), 1),
                max_rainfall = maxRain,
                min_rainfall = summary?.min_rainfall != null ? Math.Round((double)summary.min_rainfall, 1) : 0,
                rainfall_ok = maxRain <= 50.0,
                total_lightning = totalLightning,
                max_lightning_kA = Math.Round((double)(summary?.max_lightning_kA ?? 0), 1),
                lightning_ok = totalLightning <= 0,
                avg_temperature = summary?.avg_temperature != null ? Math.Round((double)summary.avg_temperature, 1) : 0,
                min_temperature = summary?.min_temperature != null ? Math.Round((double)summary.min_temperature, 1) : 0,
                max_temperature = summary?.max_temperature != null ? Math.Round((double)summary.max_temperature, 1) : 0,
            },
            trends_per_device = trendsPerDevice,
            thresholds = new
            {
                fuel_warning = 35,
                fuel_critical = 25,
                wind_warning = 35,
                wind_critical = 80,
                rainfall_warn = 30,
                rainfall_critical = 50,
                ground_warn = 3.5,
                ground_critical = 5.0,
                lightning_critical = 20,
                luminosity_warn = 900,
                luminosity_critical = 750,
                dust_warn = 35,
                dust_critical = 75,
                temperature_warn = 38,
                temperature_critical = 55
            }
        };
    }

    // ── Helper: Build trend chart data from SP rows ──
    private object BuildTrendData(IEnumerable<dynamic> trendRows)
    {
        // Group by metric, then by device
        var metrics = new Dictionary<string, Dictionary<string, List<(string label, double? val)>>>();

        foreach (var row in trendRows)
        {
            var dict = (IDictionary<string, object>)row;
            string metric = dict.ContainsKey("metric") ? dict["metric"]?.ToString() ?? "" : "";
            string device = dict.ContainsKey("device_name") ? dict["device_name"]?.ToString() ?? "" : "";
            string label = dict.ContainsKey("time_label") ? dict["time_label"]?.ToString() ?? "" : "";
            double? val = dict.ContainsKey("value") && dict["value"] != null ? Convert.ToDouble(dict["value"]) : null;

            if (string.IsNullOrEmpty(metric)) continue;

            if (!metrics.ContainsKey(metric))
                metrics[metric] = new Dictionary<string, List<(string, double?)>>();
            if (!metrics[metric].ContainsKey(device))
                metrics[metric][device] = new List<(string, double?)>();

            metrics[metric][device].Add((label, val));
        }

        object BuildForMetric(string metricName)
        {
            if (!metrics.ContainsKey(metricName))
                return new { labels = Array.Empty<string>(), devices = Array.Empty<object>() };

            var deviceMap = metrics[metricName];
            // Get labels from first device (all should be same)
            var labels = deviceMap.Values.FirstOrDefault()?.Select(x => x.label).ToArray() ?? Array.Empty<string>();

            var devices = deviceMap.Select(kv => new {
                name = kv.Key,
                values = kv.Value.Select(x => x.val).ToArray()
            }).ToArray();

            return new { labels, devices };
        }

        return new
        {
            voltage = BuildForMetric("voltage"),
            fuel_level = BuildForMetric("fuel_level"),
            wind_speed = BuildForMetric("wind_speed"),
            rainfall = BuildForMetric("rainfall"),
            luminosity = BuildForMetric("luminosity"),
            dust_level = BuildForMetric("dust_level"),
            temperature = BuildForMetric("temperature"),
            tilt = BuildForMetric("tilt"),
            ground_resistance = BuildForMetric("ground_resistance"),
            lightning_current = BuildForMetric("lightning_current"),
        };
    }

    // ── Helper: Extract tower heights from trend rows ──
    private object BuildTowerHeights(IEnumerable<dynamic> trendRows)
    {
        var heights = new Dictionary<string, double>();
        foreach (var row in trendRows)
        {
            var dict = (IDictionary<string, object>)row;
            string device = dict.ContainsKey("device_name") ? dict["device_name"]?.ToString() ?? "" : "";
            if (dict.ContainsKey("asset_height") && dict["asset_height"] != null && !string.IsNullOrEmpty(device))
            {
                try
                {
                    double h = Convert.ToDouble(dict["asset_height"]);
                    if (h > 0 && !heights.ContainsKey(device))
                        heights[device] = h;
                }
                catch { }
            }
        }
        return heights;
    }

    private static int SafeInt(dynamic? val)
    {
        if (val == null) return 0;
        try { return (int)val; } catch { return 0; }
    }

    private static void AddCategory(List<object> list, string name, int critical, int warning)
    {
        int total = critical + warning;
        if (total > 0)
        {
            list.Add(new { category = name, critical, warning, total });
        }
    }
}

