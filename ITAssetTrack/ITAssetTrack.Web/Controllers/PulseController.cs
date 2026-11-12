using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITAssetTrack.Data;
using ITAssetTrack.Models;

namespace ITAssetTrack.Controllers
{
    [Authorize]
    public class PulseController : Controller
    {
        private readonly AppDbContext _context;

        public PulseController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. DASHBOARD DEVICE ITAssetTrack PULSE
        // ==========================================
        [HttpGet("/Pulse/Dashboard")]
        [HttpGet("/Pulse")]
        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;
            var fifteenMinAgo = now.AddMinutes(-15);

            // KPI Counts
            var todayReportsCount = await _context.AgentReports
                .Where(r => r.CreatedAt >= today)
                .CountAsync();

            var uniqueDevicesToday = await _context.AgentReports
                .Where(r => r.CreatedAt >= today)
                .Select(r => !string.IsNullOrEmpty(r.Hostname) ? r.Hostname : r.SerialNumber)
                .Distinct()
                .CountAsync();

            var totalRegisteredAssets = await _context.Assets.CountAsync();
            var onlineDevices = await _context.Assets
                .Where(a => a.IsOnline == true || (a.LastOnlineTime.HasValue && a.LastOnlineTime.Value >= fifteenMinAgo))
                .CountAsync();

            var afkDevices = await _context.Assets
                .Where(a => (a.IsOnline == true || (a.LastOnlineTime.HasValue && a.LastOnlineTime.Value >= fifteenMinAgo)) && a.IsAFK == true)
                .CountAsync();

            var offlineDevices = Math.Max(0, totalRegisteredAssets - onlineDevices);

            var activeMismatchCount = await _context.AgentReports
                .Where(r => r.CreatedAt >= today && r.IsUserMismatch == true)
                .Select(r => !string.IsNullOrEmpty(r.Hostname) ? r.Hostname : r.SerialNumber)
                .Distinct()
                .CountAsync();

            var batteryHealthValues = await _context.Assets
                .Where(a => a.LastBatteryHealthPct.HasValue && a.LastBatteryHealthPct.Value > 0)
                .Select(a => a.LastBatteryHealthPct!.Value)
                .ToListAsync();

            double avgBatteryHealth = batteryHealthValues.Any() ? Math.Round(batteryHealthValues.Average(), 1) : 0;

            var totalDailyTrafficMb = await _context.AgentReports
                .Where(r => r.CreatedAt >= today && r.DailyNetworkTotalMb.HasValue)
                .SumAsync(r => r.DailyNetworkTotalMb!.Value);

            var pendingUnregistered = await _context.UnregisteredAssets
                .Where(u => u.Status != "Registered")
                .CountAsync();

            ViewBag.TodayReportsCount = todayReportsCount;
            ViewBag.UniqueDevicesToday = uniqueDevicesToday;
            ViewBag.TotalRegisteredAssets = totalRegisteredAssets;
            ViewBag.OnlineDevices = onlineDevices;
            ViewBag.AfkDevices = afkDevices;
            ViewBag.OfflineDevices = offlineDevices;
            ViewBag.ActiveMismatchCount = activeMismatchCount;
            ViewBag.AvgBatteryHealth = avgBatteryHealth;
            ViewBag.TotalDailyTrafficGb = Math.Round(totalDailyTrafficMb / 1024m, 2);
            ViewBag.PendingUnregistered = pendingUnregistered;

            return View();
        }

        // AJAX API: Live Dashboard Analytics & Metrics
        [HttpGet("/api/pulse/dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var today = DateTime.Today;
            var now = DateTime.Now;
            var fifteenMinAgo = now.AddMinutes(-15);

            var totalRegistered = await _context.Assets.CountAsync();
            var online = await _context.Assets
                .CountAsync(a => a.IsOnline == true || (a.LastOnlineTime.HasValue && a.LastOnlineTime.Value >= fifteenMinAgo));
            var afk = await _context.Assets
                .CountAsync(a => (a.IsOnline == true || (a.LastOnlineTime.HasValue && a.LastOnlineTime.Value >= fifteenMinAgo)) && a.IsAFK == true);
            var offline = Math.Max(0, totalRegistered - online);

            var todayReports = await _context.AgentReports.CountAsync(r => r.CreatedAt >= today);
            var uniqueToday = await _context.AgentReports
                .Where(r => r.CreatedAt >= today)
                .Select(r => !string.IsNullOrEmpty(r.Hostname) ? r.Hostname : r.SerialNumber)
                .Distinct()
                .CountAsync();

            var mismatchToday = await _context.AgentReports
                .Where(r => r.CreatedAt >= today && r.IsUserMismatch == true)
                .Select(r => !string.IsNullOrEmpty(r.Hostname) ? r.Hostname : r.SerialNumber)
                .Distinct()
                .CountAsync();

            var totalTrafficMb = await _context.AgentReports
                .Where(r => r.CreatedAt >= today && r.DailyNetworkTotalMb.HasValue)
                .SumAsync(r => r.DailyNetworkTotalMb!.Value);

            var unregCount = await _context.UnregisteredAssets.CountAsync(u => u.Status != "Registered");

            return Ok(new
            {
                totalRegistered,
                online,
                afk,
                offline,
                todayReports,
                uniqueToday,
                mismatchToday,
                totalTrafficGb = Math.Round(totalTrafficMb / 1024m, 2),
                unregCount
            });
        }

        // AJAX API: Normalized OS Distribution (Clean Categories)
        [HttpGet("/api/pulse/dashboard/os-distribution")]
        public async Task<IActionResult> GetOsDistribution()
        {
            // Ambil seluruh OS dari Asset dan Reports terbaru
            var rawList = await _context.Assets
                .Where(a => !string.IsNullOrEmpty(a.OperatingSystem))
                .Select(a => a.OperatingSystem)
                .ToListAsync();

            // Normalisasi kategori
            var normalizedGroups = rawList
                .Select(os =>
                {
                    string o = os.ToUpper();
                    if (o.Contains("26200") || o.Contains("25H2")) return "Windows 11 25H2";
                    if (o.Contains("26100") || o.Contains("24H2")) return "Windows 11 24H2";
                    if (o.Contains("22631") || o.Contains("23H2")) return "Windows 11 23H2";
                    if (o.Contains("22621") || o.Contains("22H2")) return "Windows 11 22H2";
                    if (o.Contains("WIN 11") || o.Contains("WINDOWS 11")) return "Windows 11 Standard/Pro";
                    if (o.Contains("WIN 10") || o.Contains("WINDOWS 10")) return "Windows 10";
                    return "Lainnya / macOS";
                })
                .GroupBy(cat => cat)
                .Select(g => new
                {
                    osName = g.Key,
                    count = g.Count()
                })
                .OrderByDescending(x => x.count)
                .ToList();

            return Ok(normalizedGroups);
        }

        // AJAX API: Hardware Load Averages
        [HttpGet("/api/pulse/dashboard/hardware-load")]
        public async Task<IActionResult> GetHardwareLoad()
        {
            var today = DateTime.Today;
            var recentReports = await _context.AgentReports
                .Where(r => r.CreatedAt >= today)
                .Select(r => new
                {
                    r.CpuMinPct, r.CpuAvgPct, r.CpuMaxPct,
                    r.RamMinPct, r.RamAvgPct, r.RamMaxPct,
                    r.GpuMinPct, r.GpuAvgPct, r.GpuMaxPct
                })
                .ToListAsync();

            if (!recentReports.Any())
            {
                recentReports = await _context.AgentReports
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(100)
                    .Select(r => new
                    {
                        r.CpuMinPct, r.CpuAvgPct, r.CpuMaxPct,
                        r.RamMinPct, r.RamAvgPct, r.RamMaxPct,
                        r.GpuMinPct, r.GpuAvgPct, r.GpuMaxPct
                    })
                    .ToListAsync();
            }

            if (!recentReports.Any())
            {
                return Ok(new
                {
                    cpu = new { min = 0, avg = 0, max = 0 },
                    ram = new { min = 0, avg = 0, max = 0 },
                    gpu = new { min = 0, avg = 0, max = 0 }
                });
            }

            var result = new
            {
                cpu = new
                {
                    min = Math.Round(recentReports.Average(r => r.CpuMinPct), 1),
                    avg = Math.Round(recentReports.Average(r => r.CpuAvgPct), 1),
                    max = Math.Round(recentReports.Average(r => r.CpuMaxPct), 1)
                },
                ram = new
                {
                    min = Math.Round(recentReports.Average(r => r.RamMinPct), 1),
                    avg = Math.Round(recentReports.Average(r => r.RamAvgPct), 1),
                    max = Math.Round(recentReports.Average(r => r.RamMaxPct), 1)
                },
                gpu = new
                {
                    min = Math.Round(recentReports.Average(r => r.GpuMinPct), 1),
                    avg = Math.Round(recentReports.Average(r => r.GpuAvgPct), 1),
                    max = Math.Round(recentReports.Average(r => r.GpuMaxPct), 1)
                }
            };

            return Ok(result);
        }

        // AJAX API: Battery Health Distribution (Horizontal Progress Tiers)
        [HttpGet("/api/pulse/dashboard/battery-distribution")]
        public async Task<IActionResult> GetBatteryDistribution()
        {
            var assetsWithBat = await _context.Assets
                .Where(a => a.LastBatteryHealthPct.HasValue && a.LastBatteryHealthPct.Value > 0)
                .Select(a => a.LastBatteryHealthPct!.Value)
                .ToListAsync();

            int good = assetsWithBat.Count(b => b >= 80);
            int fair = assetsWithBat.Count(b => b >= 50 && b < 80);
            int degraded = assetsWithBat.Count(b => b < 50);

            return Ok(new
            {
                total = assetsWithBat.Count,
                goodCount = good,
                fairCount = fair,
                degradedCount = degraded,
                goodPct = assetsWithBat.Any() ? Math.Round((double)good / assetsWithBat.Count * 100, 1) : 0,
                fairPct = assetsWithBat.Any() ? Math.Round((double)fair / assetsWithBat.Count * 100, 1) : 0,
                degradedPct = assetsWithBat.Any() ? Math.Round((double)degraded / assetsWithBat.Count * 100, 1) : 0
            });
        }

        // AJAX API: Top Daily Data Consumers (Prioritize Hostname)
        [HttpGet("/api/pulse/dashboard/top-network")]
        public async Task<IActionResult> GetTopNetworkConsumers()
        {
            var today = DateTime.Today;
            var topConsumers = await _context.AgentReports
                .Where(r => r.CreatedAt >= today && r.DailyNetworkTotalMb.HasValue && r.DailyNetworkTotalMb.Value > 0)
                .OrderByDescending(r => r.DailyNetworkTotalMb)
                .Take(5)
                .Select(r => new
                {
                    computerName = !string.IsNullOrEmpty(r.Hostname) ? r.Hostname : r.SerialNumber,
                    hostname = r.Hostname,
                    serialNumber = r.SerialNumber,
                    loggedInUser = r.LoggedInUser,
                    displayName = r.DisplayName ?? r.LoggedInUser,
                    downloadMb = r.DailyNetworkDownloadMb ?? 0,
                    uploadMb = r.DailyNetworkUploadMb ?? 0,
                    totalMb = r.DailyNetworkTotalMb ?? 0,
                    usageText = r.DailyNetworkUsageText ?? ""
                })
                .ToListAsync();

            return Ok(topConsumers);
        }

        // AJAX API: Live Pulse Recent Feed (Prioritize Hostname)
        [HttpGet("/api/pulse/dashboard/recent-feed")]
        public async Task<IActionResult> GetRecentPulseFeed()
        {
            var feed = await _context.AgentReports
                .OrderByDescending(r => r.CreatedAt)
                .Take(8)
                .Select(r => new
                {
                    r.Id,
                    computerName = !string.IsNullOrEmpty(r.Hostname) ? r.Hostname : r.SerialNumber,
                    hostname = r.Hostname,
                    serialNumber = r.SerialNumber,
                    loggedInUser = r.LoggedInUser,
                    displayName = r.DisplayName ?? r.LoggedInUser,
                    reportType = r.ReportType,
                    scheduleTriggered = r.ScheduleTriggered,
                    isUserMismatch = r.IsUserMismatch,
                    batteryHealthPct = r.BatteryHealthPct,
                    createdAt = r.CreatedAt,
                    osVersion = r.OsVersion,
                    dailyNetworkTotalMb = r.DailyNetworkTotalMb ?? 0
                })
                .ToListAsync();

            return Ok(feed);
        }

        // AJAX API: User Mismatch Security Alerts (Prioritize Hostname)
        [HttpGet("/api/pulse/dashboard/mismatch-alerts")]
        public async Task<IActionResult> GetMismatchAlerts()
        {
            var alerts = await _context.AgentReports
                .Where(r => r.IsUserMismatch == true)
                .OrderByDescending(r => r.CreatedAt)
                .Take(6)
                .Select(r => new
                {
                    r.Id,
                    computerName = !string.IsNullOrEmpty(r.Hostname) ? r.Hostname : r.SerialNumber,
                    hostname = r.Hostname,
                    serialNumber = r.SerialNumber,
                    loggedInUser = r.LoggedInUser,
                    displayName = r.DisplayName ?? r.LoggedInUser,
                    assignedNrp = r.AssignedNrp,
                    createdAt = r.CreatedAt
                })
                .ToListAsync();

            return Ok(alerts);
        }

        // =========================================================================
        // 2. LAPORAN TELEMETRI (1 DATA PER HOSTNAME - LATEST GET DATA DEPAN)
        // =========================================================================
        [HttpGet("/Pulse/Reports")]
        public async Task<IActionResult> Reports()
        {
            var today = DateTime.Today;
            var uniqueHostnamesCount = await _context.AgentReports
                .Select(r => !string.IsNullOrEmpty(r.Hostname) ? r.Hostname : r.SerialNumber)
                .Distinct()
                .CountAsync();

            var totalSnapshotsCount = await _context.AgentReports.CountAsync();
            var mismatchCount = await _context.AgentReports
                .Where(r => r.IsUserMismatch == true)
                .Select(r => !string.IsNullOrEmpty(r.Hostname) ? r.Hostname : r.SerialNumber)
                .Distinct()
                .CountAsync();

            ViewBag.UniqueHostnamesCount = uniqueHostnamesCount;
            ViewBag.TotalSnapshotsCount = totalSnapshotsCount;
            ViewBag.MismatchCount = mismatchCount;

            return View();
        }

        // Server-Side DataTables: 1 DATA PER HOSTNAME (LATEST GET DATA)
        [HttpPost("/api/pulse/reports/datatables")]
        public async Task<IActionResult> GetReportsDataTable()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var startStr = Request.Form["start"].FirstOrDefault();
            var lengthStr = Request.Form["length"].FirstOrDefault();
            var searchValue = Request.Form["search[value]"].FirstOrDefault()?.Trim().ToLower();
            var statusFilter = Request.Form["statusFilter"].FirstOrDefault()?.Trim();

            int pageSize = string.IsNullOrEmpty(lengthStr) ? 10 : Convert.ToInt32(lengthStr);
            int skip = string.IsNullOrEmpty(startStr) ? 0 : Convert.ToInt32(startStr);

            // 1) Ambil seluruh report untuk dikelompokkan per Hostname mengambil LATEST snapshot
            var allReports = await _context.AgentReports
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.Hostname,
                    r.SerialNumber,
                    r.LoggedInUser,
                    DisplayName = r.DisplayName ?? r.LoggedInUser,
                    AssignedNrp = r.AssignedNrp ?? "-",
                    r.IsUserMismatch,
                    r.OsVersion,
                    r.ReportType,
                    r.ScheduleTriggered,
                    r.BatteryHealthPct,
                    r.DailyNetworkTotalMb,
                    r.DailyNetworkUsageText,
                    r.CpuAvgPct,
                    r.RamAvgPct,
                    r.GpuAvgPct,
                    r.CreatedAt
                })
                .ToListAsync();

            // Group by Hostname (or SerialNumber if Hostname is empty)
            var grouped = allReports
                .GroupBy(r => !string.IsNullOrEmpty(r.Hostname) ? r.Hostname : r.SerialNumber)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(x => x.CreatedAt).First();
                    int totalSnapshots = g.Count();
                    bool hasMismatch = g.Any(x => x.IsUserMismatch);
                    bool hasHighLoad = g.Any(x => x.CpuAvgPct >= 85 || x.RamAvgPct >= 90);
                    bool hasDegradedBattery = latest.BatteryHealthPct.HasValue && latest.BatteryHealthPct.Value < 50;

                    string statusBadge = "Normal";
                    string statusDetails = "Seluruh parameter optimal";

                    if (latest.IsUserMismatch)
                    {
                        statusBadge = "User Mismatch";
                        statusDetails = $"NRP Login ({latest.LoggedInUser}) tidak cocok dengan tugas ({latest.AssignedNrp})";
                    }
                    else if (hasDegradedBattery)
                    {
                        statusBadge = "BMS Kritis";
                        statusDetails = $"Kesehatan BMS {latest.BatteryHealthPct}% (Degradasi)";
                    }
                    else if (latest.CpuAvgPct >= 85 || latest.RamAvgPct >= 90)
                    {
                        statusBadge = "High Load";
                        statusDetails = $"CPU {latest.CpuAvgPct}% | RAM {latest.RamAvgPct}%";
                    }

                    return new
                    {
                        id = latest.Id,
                        hostname = g.Key,
                        serialNumber = latest.SerialNumber,
                        loggedInUser = latest.LoggedInUser,
                        displayName = latest.DisplayName,
                        assignedNrp = latest.AssignedNrp,
                        isUserMismatch = latest.IsUserMismatch,
                        hasHistoricalMismatch = hasMismatch,
                        osVersion = latest.OsVersion ?? "Windows 11",
                        reportType = latest.ReportType,
                        scheduleTriggered = latest.ScheduleTriggered ?? "-",
                        batteryHealthPct = latest.BatteryHealthPct,
                        dailyNetworkTotalMb = latest.DailyNetworkTotalMb ?? 0,
                        dailyNetworkUsageText = latest.DailyNetworkUsageText ?? "-",
                        cpuAvgPct = latest.CpuAvgPct,
                        ramAvgPct = latest.RamAvgPct,
                        gpuAvgPct = latest.GpuAvgPct,
                        lastSeenAt = latest.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        totalSnapshots = totalSnapshots,
                        statusBadge = statusBadge,
                        statusDetails = statusDetails
                    };
                })
                .AsQueryable();

            // Filter status
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                if (statusFilter == "ProblemOnly")
                    grouped = grouped.Where(x => x.statusBadge != "Normal");
                else if (statusFilter == "NormalOnly")
                    grouped = grouped.Where(x => x.statusBadge == "Normal");
                else if (statusFilter == "MismatchOnly")
                    grouped = grouped.Where(x => x.isUserMismatch || x.hasHistoricalMismatch);
            }

            // Search filter
            if (!string.IsNullOrEmpty(searchValue))
            {
                grouped = grouped.Where(x =>
                    x.hostname.ToLower().Contains(searchValue) ||
                    x.serialNumber.ToLower().Contains(searchValue) ||
                    x.loggedInUser.ToLower().Contains(searchValue) ||
                    x.displayName.ToLower().Contains(searchValue) ||
                    x.assignedNrp.ToLower().Contains(searchValue) ||
                    x.osVersion.ToLower().Contains(searchValue)
                );
            }

            int recordsTotal = grouped.Count();
            int recordsFiltered = recordsTotal;

            var pagedData = grouped
                .OrderByDescending(x => x.lastSeenAt)
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                draw = draw,
                recordsTotal = recordsTotal,
                recordsFiltered = recordsFiltered,
                data = pagedData
            });
        }

        // AJAX API: Get All Historical Snapshots / Periode for a Device (Aksi Detail)
        [HttpGet("/api/pulse/reports/snapshots")]
        public async Task<IActionResult> GetDeviceSnapshots(string? hostname, string? serialNumber)
        {
            if (string.IsNullOrEmpty(hostname) && string.IsNullOrEmpty(serialNumber))
                return BadRequest(new { message = "Hostname atau Serial Number wajib disertakan" });

            var query = _context.AgentReports.AsNoTracking();

            if (!string.IsNullOrEmpty(hostname))
                query = query.Where(r => r.Hostname == hostname || r.SerialNumber == hostname);
            else if (!string.IsNullOrEmpty(serialNumber))
                query = query.Where(r => r.SerialNumber == serialNumber);

            var rawSnapshots = await query
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.ScheduleTriggered,
                    r.ReportType,
                    r.PeriodStart,
                    r.PeriodEnd,
                    r.CreatedAt,
                    r.CpuAvgPct,
                    r.RamAvgPct,
                    r.GpuAvgPct,
                    r.BatteryHealthPct,
                    r.DailyNetworkTotalMb,
                    r.IsUserMismatch
                })
                .ToListAsync();

            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            var snapshots = rawSnapshots.Select(r =>
            {
                var dt = r.CreatedAt.Date;
                string dateKey = dt.ToString("yyyy-MM-dd");
                string dateDisplay = dt == today ? "Hari ini (" + dt.ToString("dd MMM yyyy") + ")" :
                                     (dt == yesterday ? "Kemarin (" + dt.ToString("dd MMM yyyy") + ")" :
                                     dt.ToString("dd MMM yyyy"));

                string schedule = r.ScheduleTriggered ?? "-";
                string periodName = schedule == "07:59" ? "Pagi (07:59)" :
                                    schedule == "12:00" ? "Siang (12:00)" :
                                    schedule == "16:24" ? "Sore (16:24)" :
                                    (schedule == "-" ? "Sync (" + r.CreatedAt.ToString("HH:mm") + ")" : $"Sync ({schedule})");

                string status = r.IsUserMismatch 
                    ? "User Mismatch (Peringatan)" 
                    : ((r.BatteryHealthPct.HasValue && r.BatteryHealthPct.Value < 50) 
                        ? $"BMS Kritis (Baterai {r.BatteryHealthPct}%)" 
                        : (r.CpuAvgPct >= 85 
                            ? $"High CPU Load ({r.CpuAvgPct}%)" 
                            : (r.RamAvgPct >= 90 
                                ? $"High RAM Load ({r.RamAvgPct}%)" 
                                : "Normal")));

                bool isProblem = r.IsUserMismatch || 
                                 (r.BatteryHealthPct.HasValue && r.BatteryHealthPct.Value < 50) || 
                                 r.CpuAvgPct >= 85 || 
                                 r.RamAvgPct >= 90;

                return new
                {
                    id = r.Id,
                    dateKey = dateKey,
                    dateDisplay = dateDisplay,
                    periodName = periodName,
                    scheduleTriggered = schedule,
                    reportType = r.ReportType,
                    periodStart = r.PeriodStart.ToString("HH:mm"),
                    periodEnd = r.PeriodEnd.ToString("HH:mm"),
                    periodFull = $"{r.PeriodStart:dd/MM HH:mm} - {r.PeriodEnd:HH:mm}",
                    createdAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    cpuAvgPct = r.CpuAvgPct,
                    ramAvgPct = r.RamAvgPct,
                    gpuAvgPct = r.GpuAvgPct,
                    batteryHealthPct = r.BatteryHealthPct,
                    dailyNetworkTotalMb = r.DailyNetworkTotalMb ?? 0,
                    isUserMismatch = r.IsUserMismatch,
                    status = status,
                    isProblem = isProblem
                };
            }).ToList();

            return Ok(snapshots);
        }

        // AJAX API: Full-Day Aggregated Report Detail (Semua Periode dalam 1 Hari)
        [HttpGet("/api/pulse/reports/day-detail")]
        public async Task<IActionResult> GetDeviceDayDetail(string? hostname, string? serialNumber, string date)
        {
            if (string.IsNullOrEmpty(hostname) && string.IsNullOrEmpty(serialNumber))
                return BadRequest(new { message = "Hostname atau Serial Number wajib disertakan" });

            if (!DateTime.TryParse(date, out var selectedDate))
                return BadRequest(new { message = "Format tanggal tidak valid" });

            var nextDay = selectedDate.AddDays(1);

            var query = _context.AgentReports
                .Where(r => r.CreatedAt >= selectedDate && r.CreatedAt < nextDay)
                .Include(r => r.AppTimelines)
                .Include(r => r.BatteryTimelines)
                .Include(r => r.LoginSessions)
                .Include(r => r.TopApps)
                .Include(r => r.Asset)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(hostname))
                query = query.Where(r => r.Hostname == hostname || r.SerialNumber == hostname);
            else if (!string.IsNullOrEmpty(serialNumber))
                query = query.Where(r => r.SerialNumber == serialNumber);

            var dayReports = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            if (!dayReports.Any())
                return NotFound(new { message = "Tidak ada laporan telemetri pada tanggal tersebut" });

            var latest = dayReports.First();

            var rawSw = await _context.AgentInstalledSoftware
                .Where(s => s.SerialNumber == latest.SerialNumber)
                .ToListAsync();

            var installedSw = rawSw
                .GroupBy(s => s.SoftwareName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .OrderBy(s => s.SoftwareName)
                .Select(s => new
                {
                    softwareName = s.SoftwareName,
                    softwareVersion = s.SoftwareVersion ?? "-",
                    publisher = s.Publisher ?? "-",
                    installDate = s.InstallDate ?? "-"
                })
                .ToList();

            var timelines = dayReports
                .SelectMany(r => r.AppTimelines)
                .OrderBy(t => t.StartTime)
                .Select(t => new
                {
                    appName = t.AppName,
                    startTime = t.StartTime,
                    endTime = t.EndTime,
                    durationMinutes = t.DurationMinutes,
                    isAFK = t.IsAFK
                })
                .ToList();

            var batteryTimelines = dayReports
                .SelectMany(r => r.BatteryTimelines)
                .OrderBy(b => b.StartTime)
                .Select(b => new
                {
                    status = b.Status,
                    startTime = b.StartTime,
                    endTime = b.EndTime,
                    startPct = b.StartPct,
                    endPct = b.EndPct
                })
                .ToList();

            var loginSessions = dayReports
                .SelectMany(r => r.LoginSessions)
                .OrderByDescending(s => s.SessionStart)
                .Select(s => new
                {
                    hostname = s.Hostname,
                    loggedInUser = s.LoggedInUser,
                    displayName = s.DisplayName ?? s.LoggedInUser,
                    sessionStart = s.SessionStart.ToString("yyyy-MM-dd HH:mm:ss"),
                    sessionEnd = s.SessionEnd.HasValue ? s.SessionEnd.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Aktif / Realtime",
                    durationMinutes = s.DurationMinutes ?? 0
                })
                .ToList();

            var topApps = dayReports
                .SelectMany(r => r.TopApps)
                .GroupBy(a => a.AppName)
                .Select(g => new
                {
                    appName = g.Key,
                    recordCount = g.Sum(x => x.RecordCount),
                    durationMinutes = (decimal?)g.Sum(x => x.EstimatedDurationMinutes),
                    isAFK = g.All(x => x.IsAFK)
                })
                .OrderByDescending(a => a.recordCount)
                .ToList();

            TimeSpan uptimeSpan = TimeSpan.FromSeconds(latest.CpuUptimeSeconds);
            string uptimeStr = $"{uptimeSpan.Days} Hari, {uptimeSpan.Hours} Jam, {uptimeSpan.Minutes} Menit";

            string resolvedComputerName = !string.IsNullOrEmpty(latest.Hostname) 
                ? latest.Hostname 
                : (latest.Asset != null && !string.IsNullOrEmpty(latest.Asset.ComputerName) ? latest.Asset.ComputerName : latest.SerialNumber);

            bool anyMismatch = dayReports.Any(r => r.IsUserMismatch);

            var result = new
            {
                id = latest.Id,
                computerName = resolvedComputerName,
                hostname = latest.Hostname,
                serialNumber = latest.SerialNumber,
                loggedInUser = latest.LoggedInUser,
                displayName = latest.DisplayName ?? latest.LoggedInUser,
                assignedNrp = latest.AssignedNrp ?? "-",
                isUserMismatch = anyMismatch,
                osVersion = latest.OsVersion ?? "-",
                reportType = "Rekapitulasi Harian",
                scheduleTriggered = $"Semua Periode ({dayReports.Count} Snapshot)",
                recordIntervalSeconds = latest.RecordIntervalSeconds,
                periodStart = dayReports.Min(r => r.PeriodStart).ToString("yyyy-MM-dd HH:mm:ss"),
                periodEnd = dayReports.Max(r => r.PeriodEnd).ToString("yyyy-MM-dd HH:mm:ss"),
                cpuUptimeSeconds = latest.CpuUptimeSeconds,
                uptimeFormatted = uptimeStr,
                batteryHealthPct = latest.BatteryHealthPct,
                localIp = latest.LocalIp ?? "-",
                ssid = latest.Ssid ?? "-",
                latitude = latest.Latitude,
                longitude = latest.Longitude,
                coordsText = (latest.Latitude.HasValue && latest.Longitude.HasValue) ? $"{latest.Latitude.Value},{latest.Longitude.Value}" : "Unknown",
                hardware = new
                {
                    cpu = new { min = Math.Round(dayReports.Average(r => r.CpuMinPct), 1), avg = Math.Round(dayReports.Average(r => r.CpuAvgPct), 1), max = Math.Round(dayReports.Average(r => r.CpuMaxPct), 1) },
                    ram = new { min = Math.Round(dayReports.Average(r => r.RamMinPct), 1), avg = Math.Round(dayReports.Average(r => r.RamAvgPct), 1), max = Math.Round(dayReports.Average(r => r.RamMaxPct), 1) },
                    gpu = new { min = Math.Round(dayReports.Average(r => r.GpuMinPct), 1), avg = Math.Round(dayReports.Average(r => r.GpuAvgPct), 1), max = Math.Round(dayReports.Average(r => r.GpuMaxPct), 1) }
                },
                networkUsage = new
                {
                    downloadMb = latest.DailyNetworkDownloadMb ?? 0,
                    uploadMb = latest.DailyNetworkUploadMb ?? 0,
                    totalMb = latest.DailyNetworkTotalMb ?? 0,
                    text = latest.DailyNetworkUsageText ?? "Download: 0 MB | Upload: 0 MB (Total: 0 MB)"
                },
                createdAt = selectedDate.ToString("yyyy-MM-dd"),
                asset = latest.Asset == null ? null : new
                {
                    computerName = latest.Asset.ComputerName,
                    model = latest.Asset.Model,
                    brand = latest.Asset.Brand,
                    site = latest.Asset.Site,
                    division = latest.Asset.Division,
                    department = latest.Asset.Department,
                    assignedTo = latest.Asset.AssignedTo,
                    nrp = latest.Asset.NRP
                },
                appTimelines = timelines,
                installedSoftware = installedSw,
                batteryTimelines = batteryTimelines,
                loginSessions = loginSessions,
                topApps = topApps
            };

            return Ok(result);
        }

        // AJAX API: Complete 6-Tab Report Detail Drilldown (for a specific snapshot)
        [HttpGet("/api/pulse/reports/detail/{id}")]
        public async Task<IActionResult> GetReportDetail(Guid id)
        {
            var report = await _context.AgentReports
                .Include(r => r.AppTimelines)
                .Include(r => r.BatteryTimelines)
                .Include(r => r.LoginSessions)
                .Include(r => r.TopApps)
                .Include(r => r.Asset)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound(new { message = "Laporan telemetri tidak ditemukan" });

            // Ambil installed software untuk perangkat ini (Deduplicated latest software snapshot)
            var rawSw = await _context.AgentInstalledSoftware
                .Where(s => s.SerialNumber == report.SerialNumber)
                .ToListAsync();

            var installedSw = rawSw
                .GroupBy(s => s.SoftwareName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .OrderBy(s => s.SoftwareName)
                .Select(s => new
                {
                    softwareName = s.SoftwareName,
                    softwareVersion = s.SoftwareVersion ?? "-",
                    publisher = s.Publisher ?? "-",
                    installDate = s.InstallDate ?? "-"
                })
                .ToList();

            var timelines = report.AppTimelines
                .OrderBy(t => t.StartTime)
                .Select(t => new
                {
                    appName = t.AppName,
                    startTime = t.StartTime,
                    endTime = t.EndTime,
                    durationMinutes = t.DurationMinutes,
                    isAFK = t.IsAFK
                })
                .ToList();

            var batteryTimelines = report.BatteryTimelines
                .OrderBy(b => b.StartTime)
                .Select(b => new
                {
                    status = b.Status,
                    startTime = b.StartTime,
                    endTime = b.EndTime,
                    startPct = b.StartPct,
                    endPct = b.EndPct
                })
                .ToList();

            var loginSessions = report.LoginSessions
                .OrderByDescending(s => s.SessionStart)
                .Select(s => new
                {
                    hostname = s.Hostname,
                    loggedInUser = s.LoggedInUser,
                    displayName = s.DisplayName ?? s.LoggedInUser,
                    sessionStart = s.SessionStart.ToString("yyyy-MM-dd HH:mm:ss"),
                    sessionEnd = s.SessionEnd.HasValue ? s.SessionEnd.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Aktif / Realtime",
                    durationMinutes = s.DurationMinutes ?? 0
                })
                .ToList();

            var topApps = report.TopApps
                .OrderByDescending(a => a.RecordCount)
                .Select(a => new
                {
                    appName = a.AppName,
                    recordCount = a.RecordCount,
                    durationMinutes = a.EstimatedDurationMinutes,
                    isAFK = a.IsAFK
                })
                .ToList();

            TimeSpan uptimeSpan = TimeSpan.FromSeconds(report.CpuUptimeSeconds);
            string uptimeStr = $"{uptimeSpan.Days} Hari, {uptimeSpan.Hours} Jam, {uptimeSpan.Minutes} Menit";

            string resolvedComputerName = !string.IsNullOrEmpty(report.Hostname) 
                ? report.Hostname 
                : (report.Asset != null && !string.IsNullOrEmpty(report.Asset.ComputerName) ? report.Asset.ComputerName : report.SerialNumber);

            var result = new
            {
                id = report.Id,
                computerName = resolvedComputerName,
                hostname = report.Hostname,
                serialNumber = report.SerialNumber,
                loggedInUser = report.LoggedInUser,
                displayName = report.DisplayName ?? report.LoggedInUser,
                assignedNrp = report.AssignedNrp ?? "-",
                isUserMismatch = report.IsUserMismatch,
                osVersion = report.OsVersion ?? "-",
                reportType = report.ReportType,
                scheduleTriggered = report.ScheduleTriggered ?? "-",
                recordIntervalSeconds = report.RecordIntervalSeconds,
                periodStart = report.PeriodStart.ToString("yyyy-MM-dd HH:mm:ss"),
                periodEnd = report.PeriodEnd.ToString("yyyy-MM-dd HH:mm:ss"),
                cpuUptimeSeconds = report.CpuUptimeSeconds,
                uptimeFormatted = uptimeStr,
                batteryHealthPct = report.BatteryHealthPct,
                localIp = report.LocalIp ?? "-",
                ssid = report.Ssid ?? "-",
                latitude = report.Latitude,
                longitude = report.Longitude,
                coordsText = (report.Latitude.HasValue && report.Longitude.HasValue) ? $"{report.Latitude.Value},{report.Longitude.Value}" : "Unknown",
                hardware = new
                {
                    cpu = new { min = report.CpuMinPct, avg = report.CpuAvgPct, max = report.CpuMaxPct },
                    ram = new { min = report.RamMinPct, avg = report.RamAvgPct, max = report.RamMaxPct },
                    gpu = new { min = report.GpuMinPct, avg = report.GpuAvgPct, max = report.GpuMaxPct }
                },
                networkUsage = new
                {
                    downloadMb = report.DailyNetworkDownloadMb ?? 0,
                    uploadMb = report.DailyNetworkUploadMb ?? 0,
                    totalMb = report.DailyNetworkTotalMb ?? 0,
                    text = report.DailyNetworkUsageText ?? "Download: 0 MB | Upload: 0 MB (Total: 0 MB)"
                },
                createdAt = report.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                asset = report.Asset == null ? null : new
                {
                    computerName = report.Asset.ComputerName,
                    model = report.Asset.Model,
                    brand = report.Asset.Brand,
                    site = report.Asset.Site,
                    division = report.Asset.Division,
                    department = report.Asset.Department,
                    assignedTo = report.Asset.AssignedTo,
                    nrp = report.Asset.NRP
                },
                appTimelines = timelines,
                installedSoftware = installedSw,
                batteryTimelines = batteryTimelines,
                loginSessions = loginSessions,
                topApps = topApps
            };

            return Ok(result);
        }

        // ==========================================
        // 3. AUDIT PERANGKAT BELUM TERDAFTAR (UNREGISTERED ASSETS)
        // ==========================================
        [HttpGet("/Pulse/Unregistered")]
        public async Task<IActionResult> Unregistered()
        {
            ViewBag.TotalUnregistered = await _context.UnregisteredAssets.CountAsync();
            ViewBag.PendingCount = await _context.UnregisteredAssets.CountAsync(u => u.Status != "Registered");
            ViewBag.RegisteredCount = await _context.UnregisteredAssets.CountAsync(u => u.Status == "Registered");

            return View();
        }

        // AJAX DataTables for Unregistered Assets (Prioritize Hostname)
        [HttpPost("/api/pulse/unregistered/datatables")]
        public async Task<IActionResult> GetUnregisteredDataTable()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var startStr = Request.Form["start"].FirstOrDefault();
            var lengthStr = Request.Form["length"].FirstOrDefault();
            var searchValue = Request.Form["search[value]"].FirstOrDefault()?.Trim().ToLower();
            var statusFilter = Request.Form["statusFilter"].FirstOrDefault()?.Trim();

            int pageSize = string.IsNullOrEmpty(lengthStr) ? 10 : Convert.ToInt32(lengthStr);
            int skip = string.IsNullOrEmpty(startStr) ? 0 : Convert.ToInt32(startStr);

            var query = _context.UnregisteredAssets.AsNoTracking();

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                query = query.Where(u => u.Status == statusFilter);
            }

            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(u =>
                    u.Hostname.ToLower().Contains(searchValue) ||
                    u.SerialNumber.ToLower().Contains(searchValue) ||
                    u.LoggedInUser.ToLower().Contains(searchValue) ||
                    (u.OsVersion != null && u.OsVersion.ToLower().Contains(searchValue)) ||
                    (u.LocalIp != null && u.LocalIp.ToLower().Contains(searchValue))
                );
            }

            int recordsTotal = await _context.UnregisteredAssets.CountAsync();
            int recordsFiltered = await query.CountAsync();

            var data = await query
                .OrderByDescending(u => u.LastSeenAt)
                .Skip(skip)
                .Take(pageSize)
                .Select(u => new
                {
                    id = u.Id,
                    computerName = !string.IsNullOrEmpty(u.Hostname) ? u.Hostname : u.SerialNumber,
                    hostname = u.Hostname,
                    serialNumber = u.SerialNumber,
                    loggedInUser = u.LoggedInUser,
                    osVersion = u.OsVersion ?? "-",
                    localIp = u.LocalIp ?? "-",
                    ssid = u.Ssid ?? "-",
                    coords = (u.Latitude.HasValue && u.Longitude.HasValue) ? $"{u.Latitude.Value},{u.Longitude.Value}" : "-",
                    lastSeenAt = u.LastSeenAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    reportCount = u.ReportCount,
                    status = u.Status
                })
                .ToListAsync();

            return Ok(new
            {
                draw = draw,
                recordsTotal = recordsTotal,
                recordsFiltered = recordsFiltered,
                data = data
            });
        }

        // Quick Register Unregistered Asset to tbl_t_asset
        [HttpPost("/api/pulse/unregistered/quick-register")]
        [Authorize(Roles = "Admin,Coordinator")]
        public async Task<IActionResult> QuickRegister([FromBody] QuickRegisterModel model)
        {
            if (string.IsNullOrEmpty(model.SerialNumber))
                return BadRequest(new { message = "Serial number wajib diisi" });

            var unreg = await _context.UnregisteredAssets.FirstOrDefaultAsync(u => u.SerialNumber == model.SerialNumber);
            var existingAsset = await _context.Assets.FirstOrDefaultAsync(a => a.SerialNumber == model.SerialNumber);

            if (existingAsset != null)
            {
                if (unreg != null)
                {
                    unreg.Status = "Registered";
                    unreg.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
                return Ok(new { message = "Perangkat sudah terdaftar di master asset" });
            }

            var newAsset = new Asset
            {
                Id = Guid.NewGuid(),
                SerialNumber = model.SerialNumber,
                ComputerName = model.Hostname ?? model.SerialNumber,
                Model = model.Model ?? "Laptop (Auto-Detected)",
                OperatingSystem = model.OsVersion ?? "Windows 11",
                Status = "InUse",
                LastOnlineTime = DateTime.Now,
                LastIp = unreg?.LocalIp ?? "",
                LastLoggedInUser = unreg?.LoggedInUser ?? "",
                IsOnline = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Assets.Add(newAsset);

            if (unreg != null)
            {
                unreg.Status = "Registered";
                unreg.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = $"Perangkat {newAsset.ComputerName} ({model.SerialNumber}) berhasil didaftarkan ke Master Asset" });
        }
    }

    public class QuickRegisterModel
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string? Hostname { get; set; }
        public string? OsVersion { get; set; }
        public string? Model { get; set; }
    }
}
