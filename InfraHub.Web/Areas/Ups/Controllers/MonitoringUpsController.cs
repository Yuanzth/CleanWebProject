using InfraHub.Core.Interfaces;
using InfraHub.Infrastructure.Data;
using InfraHub.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InfraHub.Web.Areas.Ups.Controllers;

[Area("Ups")]
[Authorize]
[Route("[area]/Monitoring")]
public class MonitoringUpsController : Controller
{
    private readonly IStoredProcedureExecutor _spExecutor;
    private readonly AppDbContext _context;

    public MonitoringUpsController(IStoredProcedureExecutor spExecutor, AppDbContext context)
    {
        _spExecutor = spExecutor;
        _context = context;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var roleName = User.FindFirst("RoleName")?.Value ?? "";
        ViewBag.IsAdmin = roleName == "Administrator";
        return View();
    }

    [HttpGet("/api/ups/sites")]
    public async Task<IActionResult> GetSites([FromQuery] int only_with_ups = 1)
    {
        try
        {
            var parameters = new Dictionary<string, object?> { { "OnlyWithUps", only_with_ups } };
            var sites = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetUpsSites", parameters);

            return Json(new {
                status = "success",
                message = "UPS sites retrieved successfully",
                meta = new {
                    total_sites = sites.Count(),
                    filter = only_with_ups == 1 ? "Only sites with UPS" : "All sites",
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                },
                data = sites
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                status = "error",
                message = "Failed to retrieve UPS sites",
                error = ex.Message
            });
        }
    }

    [HttpGet("/api/ups/devices")]
    public async Task<IActionResult> GetDevices([FromQuery] string site_id)
    {
        try
        {
            if (string.IsNullOrEmpty(site_id))
            {
                return BadRequest(new { status = "error", message = "site_id parameter is required" });
            }

            var parameters = new Dictionary<string, object?> { { "SiteID", site_id } };
            var devices = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetUpsDevicesBySite", parameters);
            
            return Json(new {
                status = "success",
                message = "UPS devices retrieved successfully",
                meta = new {
                    total_devices = devices.Count(),
                    site_id = site_id,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                },
                data = devices
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                status = "error",
                message = "Failed to retrieve UPS devices",
                error = ex.Message
            });
        }
    }

    [HttpGet("/api/ups/latest")]
    public async Task<IActionResult> GetLatestUpsStatus([FromQuery] string? device_id, [FromQuery] string? site_id, [FromQuery] string? zone_id)
    {
        try
        {
            var parameters = new Dictionary<string, object?>
            {
                { "DeviceID", device_id },
                { "SiteID", site_id },
                { "ZoneID", zone_id }
            };

            var latestStatus = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetLatestUpsStatus", parameters);
            var dataObj = latestStatus.FirstOrDefault();

            IDictionary<string, object>? data = null;
            if (dataObj != null)
            {
                data = new Dictionary<string, object>((IDictionary<string, object>)dataObj, StringComparer.OrdinalIgnoreCase);

                string? devIdStr = device_id;
                if (string.IsNullOrEmpty(devIdStr))
                {
                    if (data.TryGetValue("device_id", out var dIdVal) && dIdVal != null) devIdStr = dIdVal.ToString();
                    else if (data.TryGetValue("id", out var idVal) && idVal != null) devIdStr = idVal.ToString();
                }

                if (!string.IsNullOrEmpty(devIdStr) && Guid.TryParse(devIdStr, out var devGuid))
                {
                    var sqlLastOnline = @"
                        SELECT TOP 1 device_time 
                        FROM tbl_t_ups_logs 
                        WHERE device_id = @p0 
                          AND ISNULL(ups_status, '') NOT IN ('Offline', 'Connection Error', 'Auth Error', 'System Error', '')
                          AND (voltage_in_a IS NOT NULL OR voltage_out_a IS NOT NULL OR battery_voltage IS NOT NULL)
                        ORDER BY device_time DESC, created_at DESC";
                    var loRes = await _spExecutor.ExecuteSqlAsync(sqlLastOnline, new object[] { devGuid });
                    var loRow = loRes.FirstOrDefault() as IDictionary<string, object>;
                    if (loRow != null && loRow.ContainsKey("device_time") && loRow["device_time"] != null)
                    {
                        data["last_online_time"] = loRow["device_time"];
                    }
                }
            }

            return Json(new {
                status = "success",
                message = "Latest UPS status retrieved successfully",
                meta = new {
                    filters = new { device_id, site_id, zone_id },
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                },
                data = data
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                status = "error",
                message = "Failed to retrieve latest UPS status",
                error = ex.Message
            });
        }
    }

    [HttpGet("/api/ups/history")]
    public async Task<IActionResult> GetVoltageHistory([FromQuery] string device_id, [FromQuery] int hours = 24, [FromQuery] int limit = 96)
    {
        try
        {
            if (string.IsNullOrEmpty(device_id))
            {
                return BadRequest(new { status = "error", message = "device_id parameter is required" });
            }

            var parameters = new Dictionary<string, object?>
            {
                { "DeviceID", device_id },
                { "Hours", hours },
                { "Limit", limit }
            };

            var history = await _spExecutor.ExecuteReaderAsync<dynamic>("sp_GetUpsVoltageHistory", parameters);

            return Json(new {
                status = "success",
                message = "Voltage history retrieved successfully",
                meta = new {
                    total_records = history.Count(),
                    device_id = device_id,
                    hours_range = hours,
                    limit = limit,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                },
                data = history
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                status = "error",
                message = "Failed to retrieve voltage history",
                error = ex.Message
            });
        }
    }

    [HttpPost("/api/ups/acknowledge")]
    public async Task<IActionResult> SubmitAcknowledge([FromForm] string deviceId, [FromForm] DateTime startTime, [FromForm] DateTime endTime, [FromForm] string reason, [FromForm] string? notes, IFormFile? evidence)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId")?.Value ?? User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
            {
                return Unauthorized(new { status = "error", message = "User not authenticated or invalid user ID format." });
            }

            if (string.IsNullOrEmpty(deviceId))
                return BadRequest(new { status = "error", message = "Device ID is required." });

            if (string.IsNullOrEmpty(reason))
                return BadRequest(new { status = "error", message = "Reason is required." });

            if (endTime <= startTime)
                return BadRequest(new { status = "error", message = "Waktu Selesai harus lebih besar dari Waktu Mulai." });

            string? evidencePath = null;
            if (evidence != null && evidence.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "evidence");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(evidence.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await evidence.CopyToAsync(stream);
                }
                evidencePath = $"/uploads/evidence/{fileName}";
            }

            var ack = new DeviceAcknowledge
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                AcknowledgedBy = userId,
                StartTime = startTime,
                EndTime = endTime,
                Reason = reason,
                Notes = notes,
                EvidencePath = evidencePath,
                VerificationStatus = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.DeviceAcknowledges.Add(ack);
            await _context.SaveChangesAsync();

            return Json(new { status = "success", message = "Acknowledge berhasil diajukan dan menunggu verifikasi Tim DCO.", id = ack.Id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = "Failed to submit acknowledgement: " + ex.Message });
        }
    }

    [HttpGet("/api/ups/acknowledge/list")]
    public async Task<IActionResult> GetAcknowledgeList([FromQuery] string? status, [FromQuery] string? site_id, [FromQuery] string? date_from, [FromQuery] string? date_to)
    {
        try
        {
            var query = _context.DeviceAcknowledges
                .Include(a => a.User)
                .Include(a => a.Verifier)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(a => a.VerificationStatus == status);
            }

            if (!string.IsNullOrEmpty(date_from) && DateTime.TryParse(date_from, out var fromDt))
            {
                query = query.Where(a => a.StartTime >= fromDt.Date);
            }

            if (!string.IsNullOrEmpty(date_to) && DateTime.TryParse(date_to, out var toDt))
            {
                var endDay = toDt.Date.AddDays(1);
                query = query.Where(a => a.StartTime < endDay);
            }

            var acks = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

            // Load devices and sites with lean projection for metadata mapping
            var devList = await _context.Devices
                .Select(d => new {
                    IdStr = d.Id.ToString().ToLower(),
                    DeviceName = d.DeviceName,
                    SiteIdStr = d.SiteId.HasValue ? d.SiteId.Value.ToString() : "",
                    SiteCode = d.Site != null ? d.Site.SiteCode : "-",
                    SiteName = d.Site != null ? d.Site.SiteName : "-",
                    AssetType = d.AssetType != null ? d.AssetType.TypeName : "UPS"
                })
                .ToListAsync();

            var devMap = devList.ToDictionary(d => d.IdStr, d => d);

            var items = acks.Select(a => {
                string devIdLower = (a.DeviceId ?? "").ToLower();
                devMap.TryGetValue(devIdLower, out var dev);

                string siteCode = dev?.SiteCode ?? "-";
                string siteName = dev?.SiteName ?? "-";
                string siteIdStr = dev?.SiteIdStr ?? "";
                string devName = !string.IsNullOrEmpty(dev?.DeviceName) ? dev.DeviceName : a.DeviceId;
                string assetType = dev?.AssetType ?? "UPS";

                return new {
                    id = a.Id,
                    device_id = a.DeviceId,
                    device_name = devName,
                    site_id = siteIdStr,
                    site_code = siteCode,
                    site_name = siteName,
                    asset_type = assetType,
                    start_time = a.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    end_time = a.EndTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    duration_minutes = Math.Max(0, (int)(a.EndTime - a.StartTime).TotalMinutes),
                    reason = a.Reason,
                    notes = a.Notes,
                    evidence_path = a.EvidencePath,
                    verification_status = a.VerificationStatus,
                    created_at = a.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    acknowledged_by_id = a.AcknowledgedBy,
                    acknowledged_by_name = a.User?.Name ?? a.User?.Username ?? "User",
                    verified_by_id = a.VerifiedBy,
                    verified_by_name = a.Verifier?.Name ?? a.Verifier?.Username ?? "-",
                    verified_at = a.VerifiedAt.HasValue ? a.VerifiedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    verification_notes = a.VerificationNotes
                };
            }).ToList();

            if (!string.IsNullOrEmpty(site_id) && !string.Equals(site_id, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(i => string.Equals(i.site_id, site_id, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Calculate overall global stats across database (or filtered by site if site is selected)
            var allAcksQuery = _context.DeviceAcknowledges.AsQueryable();
            var allAcksList = await allAcksQuery.Select(a => new { a.VerificationStatus, a.DeviceId }).ToListAsync();

            int globalTotal = allAcksList.Count;
            int globalPending = allAcksList.Count(i => i.VerificationStatus == "Pending");
            int globalVerified = allAcksList.Count(i => i.VerificationStatus == "Verified" || i.VerificationStatus == "Approved");
            int globalRejected = allAcksList.Count(i => i.VerificationStatus == "Rejected");

            return Json(new {
                status = "success",
                data = items,
                total = globalTotal,
                pending_count = globalPending,
                verified_count = globalVerified,
                rejected_count = globalRejected,
                filtered_count = items.Count
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = "Failed to load acknowledge list: " + ex.Message });
        }
    }

    [HttpPost("/api/ups/acknowledge/verify")]
    public async Task<IActionResult> VerifyAcknowledge([FromForm] Guid id, [FromForm] string action, [FromForm] string? notes)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId")?.Value ?? User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
            {
                return Unauthorized(new { status = "error", message = "User not authenticated." });
            }

            var ack = await _context.DeviceAcknowledges.FindAsync(id);
            if (ack == null)
            {
                return NotFound(new { status = "error", message = "Data Acknowledge tidak ditemukan." });
            }

            string statusResult = action.ToLower() switch
            {
                "verified" or "approve" or "approved" => "Verified",
                "rejected" or "reject" => "Rejected",
                _ => "Pending"
            };

            ack.VerificationStatus = statusResult;
            ack.VerifiedBy = userId;
            ack.VerifiedAt = DateTime.UtcNow;
            ack.VerificationNotes = notes;

            await _context.SaveChangesAsync();

            var verifierUser = await _context.Users.FindAsync(userId);

            return Json(new {
                status = "success",
                message = statusResult == "Verified" ? "Acknowledge berhasil diverifikasi dan disetujui." : "Acknowledge berhasil ditolak.",
                data = new {
                    id = ack.Id,
                    verification_status = ack.VerificationStatus,
                    verified_by_name = verifierUser?.Name ?? verifierUser?.Username ?? "DCO Team",
                    verified_at = ack.VerifiedAt.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                    verification_notes = ack.VerificationNotes
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = "Failed to verify acknowledge: " + ex.Message });
        }
    }

    [HttpGet("/api/ups/acknowledge/pending-count")]
    public async Task<IActionResult> GetPendingAcknowledgeCount()
    {
        try
        {
            var count = await _context.DeviceAcknowledges
                .CountAsync(a => a.VerificationStatus == "Pending");

            return Json(new { status = "success", count = count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }
}





