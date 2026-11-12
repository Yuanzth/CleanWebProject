using InfraHub.Core.Interfaces;
using InfraHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraHub.Web.Areas.Ups.Controllers;

[Area("Ups")]
[Authorize]
[Route("[area]/Manage")]
public class ManageUpsDeviceController : Controller
{
    private readonly AppDbContext _context;

    public ManageUpsDeviceController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var sites = _context.Sites.OrderBy(s => s.SiteCode).ToList();
        ViewBag.Sites = sites;
        
        // Pre-compute device counts per site for sidebar badges
        var validTypes = new[] { "UPS", "CCTV", "PAC", "SENSOR" };
        var siteDeviceCounts = _context.Devices
            .Where(d => d.AssetType != null && validTypes.Contains(d.AssetType.TypeName.ToUpper()))
            .GroupBy(d => d.SiteId)
            .Select(g => new { SiteId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.SiteId, x => x.Count);
        ViewBag.SiteDeviceCounts = siteDeviceCounts;
        
        return View();
    }

    [HttpGet("/api/ups/manage/devices")]
    public IActionResult GetDevicesDatatable([FromQuery] string? siteId)
    {
        // Quick self-healing for devices that lost their UPS info due to model binding error
        var brokenDevices = _context.Devices.Where(d => d.AssetType.TypeName.ToUpper() == "UPS" && d.UpsPhaseType == null).ToList();
        if (brokenDevices.Any())
        {
            foreach(var bd in brokenDevices)
            {
                bd.UpsPhaseType = "3/3";
                bd.UpsType = "EATON 9355";
                bd.UpsCategory = "UPS DC";
            }
            _context.SaveChanges();
        }

        // Quick self-healing for legacy Sensor categories
        var legacySensors = _context.Devices.Where(d => d.UpsCategory == "SENSOR SUHU RUANGAN POWER" || d.UpsCategory == "SENSOR SUHU RUANGAN DC").ToList();
        if (legacySensors.Any())
        {
            foreach (var ls in legacySensors)
            {
                ls.UpsCategory = "SENSOR SUHU RUANGAN";
            }
            _context.SaveChanges();
        }

        var validTypes = new[] { "UPS", "CCTV", "PAC", "SENSOR" };
        var query = _context.Devices.Where(d => d.AssetType != null && validTypes.Contains(d.AssetType.TypeName.ToUpper()));

        if (!string.IsNullOrEmpty(siteId) && Guid.TryParse(siteId, out Guid sId))
        {
            query = query.Where(d => d.SiteId == sId);
        }

        var devices = query
            .OrderBy(d => d.CreatedAt)
            .Select(d => new
            {
                id = d.Id,
                device_name = d.DeviceName,
                serial_number = d.SerialNumber,
                ip_address = d.IpAddress,
                asset_type = d.AssetType.TypeName,
                ups_type = d.UpsType,
                ups_phase_type = d.UpsPhaseType,
                ups_category = d.UpsCategory,
                is_active = d.IsActive,
                site_code = d.Site != null ? d.Site.SiteCode : "-",
                site_name = d.Site != null ? d.Site.SiteName : ""
            })
            .ToList();

        var formattedData = devices.Select((d, index) =>
        {
            var catLabel = (d.ups_category ?? "").Trim().ToUpper() switch {
                "SENSOR SUHU RUANGAN" => "Sensor Suhu",
                "PAC RUANGAN DC" => "PAC",
                "CCTV AVAILBILITY" => "CCTV",
                "UPS DC" => "UPS DC",
                "UPS USER" => "UPS User",
                _ => d.asset_type ?? "UPS"
            };
            var catColor = (d.asset_type ?? "UPS").Trim().ToUpper() switch {
                "SENSOR" => "#f59e0b",
                "PAC" => "#10b981",
                "CCTV" => "#8b5cf6",
                _ => "#3b82f6"
            };
            return new {
                DT_RowIndex = index + 1,
                id = d.id,
                device_name = $"<div class='udt-dev-name'>{d.device_name}</div><small class='udt-dev-site'>{d.site_code} — {d.site_name}</small>",
                category_badge = $"<span class='udt-cat-badge' style='--cat-color:{catColor};'>{catLabel}</span>",
                ip_col = string.IsNullOrEmpty(d.ip_address) ? "<span class='udt-no-data'>—</span>" : $"<span class='udt-ip'>{d.ip_address}</span>",
                status_badge = d.is_active 
                    ? "<span class='udt-status-badge udt-status-active'><i class='fa-solid fa-circle'></i> Active</span>"
                    : "<span class='udt-status-badge udt-status-inactive'><i class='fa-solid fa-circle'></i> Inactive</span>",
                action = $@"<div class='udt-action-group'><button type='button' class='udt-btn-edit' data-id='{d.id}'><i class='fa-solid fa-pen-to-square'></i></button>
                            <button type='button' class='udt-btn-delete' data-id='{d.id}' data-name='{d.device_name}'><i class='fa-solid fa-trash'></i></button></div>",
                // Raw fields for Card View + Stats
                ups_category = d.ups_category ?? "",
                asset_type = d.asset_type ?? "",
                is_active = d.is_active,
                ip_address = d.ip_address ?? "",
                ups_type = d.ups_type ?? "",
                serial_number = d.serial_number ?? "",
                site_code = d.site_code ?? "",
                site_name = d.site_name ?? ""
            };
        });

        return Json(new { data = formattedData }, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null });
    }

    private string GetPhaseBadge(string? pt)
    {
        pt ??= "3/3";
        var color = pt switch {
            "3/3" => "phase-33",
            "3/1" => "phase-31",
            "1/1" => "phase-11",
            _ => "phase-33"
        };
        var label = pt switch {
            "3/3" => "3Ø / 3Ø",
            "3/1" => "3Ø / 1Ø",
            "1/1" => "1Ø / 1Ø",
            _ => pt
        };
        return $"<span class='udt-phase-badge {color}'>{label}</span>";
    }

    private string GetUpsTypeBadge(string? assetType, string? type, string? category)
    {
        var at = (assetType ?? "UPS").Trim().ToUpper();
        if (at != "UPS")
        {
            var label = (category ?? at).Trim().ToUpper();
            var badgeColor = at switch {
                "SENSOR" => "#10b981",
                "PAC" => "#3b82f6",
                "CCTV" => "#8b5cf6",
                _ => "#6b7280"
            };
            return $"<span class='udt-phase-badge' style='background: {badgeColor}; margin-bottom: 5px;'>{at}</span><br><span class='udt-status-badge' style='background: rgba(255,255,255,0.1); color: #ccc; border: 1px solid rgba(255,255,255,0.2); font-size: 0.75rem; padding: 2px 6px;'>{label}</span>";
        }

        var t = (type ?? "EATON 9355").Trim().ToUpper();
        var c = (category ?? "UPS DC").Trim().ToUpper();
        var cssClass = t switch {
            "EATON 9PX" => "phase-31",
            "EATON 9155" => "phase-31",
            "TROY-X" => "phase-11",
            _ => "phase-33"
        };
        return $"<span class='udt-phase-badge {cssClass}' style='margin-bottom: 5px;'>{t}</span><br><span class='udt-status-badge' style='background: rgba(255,255,255,0.1); color: #ccc; border: 1px solid rgba(255,255,255,0.2); font-size: 0.75rem; padding: 2px 6px;'>{c}</span>";
    }

    [HttpPost("/api/ups/manage/devices")]
    public IActionResult Store([FromForm] InfraHub.Infrastructure.Data.Entities.Device request, [FromForm] string deviceCategoryType)
    {
        // For non-UPS devices, SerialNumber might not be provided in the form.
        // We auto-generate it if missing because DB requires it.
        var serialNumber = string.IsNullOrEmpty(request.SerialNumber) ? $"AUTO-{Guid.NewGuid().ToString().Substring(0,8).ToUpper()}" : request.SerialNumber;
        
        if (_context.Devices.Any(d => d.Imei == serialNumber))
        {
            return StatusCode(422, new { message = "Serial Number sudah dipakai sebagai identitas perangkat (IMEI). Gunakan Serial Number lain." });
        }

        // Map Category to AssetType
        string targetAssetTypeName = deviceCategoryType switch {
            "CCTV AVAILBILITY" => "CCTV",
            "PAC RUANGAN DC" => "PAC",
            "SENSOR SUHU RUANGAN" => "SENSOR",
            _ => "UPS"
        };

        var assetType = _context.AssetTypes.FirstOrDefault(a => a.TypeName.ToUpper() == targetAssetTypeName);
        if (assetType == null) 
        {
            assetType = new InfraHub.Infrastructure.Data.Entities.AssetType { Id = Guid.NewGuid(), TypeName = targetAssetTypeName };
            _context.AssetTypes.Add(assetType);
            _context.SaveChanges();
        }

        var newDevice = new InfraHub.Infrastructure.Data.Entities.Device
        {
            Id = Guid.NewGuid(),
            DeviceName = request.DeviceName,
            SerialNumber = serialNumber,
            Imei = serialNumber,
            SiteId = request.SiteId,
            AssetTypeId = assetType.Id,
            UpsCategory = deviceCategoryType,
            IpAddress = request.IpAddress,
            UpsUsername = request.UpsUsername,
            UpsPassword = request.UpsPassword,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (targetAssetTypeName == "UPS")
        {
            newDevice.UpsPhaseType = request.UpsPhaseType;
            newDevice.UpsType = request.UpsType;
            newDevice.RoomSensorIp = request.RoomSensorIp;
        }

        _context.Devices.Add(newDevice);
        _context.SaveChanges();

        return Json(new { success = "UPS device berhasil ditambahkan." });
    }

    [HttpGet("/api/ups/manage/devices/{id}")]
    public IActionResult Edit(string id)
    {
        if (!Guid.TryParse(id, out Guid guidId)) return NotFound();
        var device = _context.Devices.FirstOrDefault(d => d.Id == guidId);
        if (device == null) return NotFound();

        return Json(new
        {
            id = device.Id,
            device_name = device.DeviceName,
            serial_number = device.SerialNumber,
            ip_address = device.IpAddress,
            room_sensor_ip = device.RoomSensorIp,
            site_id = device.SiteId,
            ups_phase_type = device.UpsPhaseType ?? "3/3",
            ups_type = device.UpsType ?? "EATON 9355",
            ups_category = device.UpsCategory ?? "UPS DC",
            ups_username = device.UpsUsername,
            ups_password = device.UpsPassword,
            is_active = device.IsActive
        });
    }

    [HttpPut("/api/ups/manage/devices/{id}")]
    public IActionResult Update(string id, [FromForm] InfraHub.Infrastructure.Data.Entities.Device request, [FromForm] string deviceCategoryType)
    {
        if (!Guid.TryParse(id, out Guid guidId)) return NotFound();

        var serialNumber = string.IsNullOrEmpty(request.SerialNumber) ? $"AUTO-{Guid.NewGuid().ToString().Substring(0,8).ToUpper()}" : request.SerialNumber;

        if (_context.Devices.Any(d => d.Imei == serialNumber && d.Id != guidId))
        {
            return StatusCode(422, new { message = "Serial Number sudah dipakai sebagai identitas perangkat (IMEI). Gunakan Serial Number lain." });
        }

        var device = _context.Devices.FirstOrDefault(d => d.Id == guidId);
        if (device == null) return NotFound();
        
        string targetAssetTypeName = deviceCategoryType switch {
            "CCTV AVAILBILITY" => "CCTV",
            "PAC RUANGAN DC" => "PAC",
            "SENSOR SUHU RUANGAN" => "SENSOR",
            _ => "UPS"
        };
        var assetType = _context.AssetTypes.FirstOrDefault(a => a.TypeName.ToUpper() == targetAssetTypeName);
        if (assetType == null) 
        {
            assetType = new InfraHub.Infrastructure.Data.Entities.AssetType { Id = Guid.NewGuid(), TypeName = targetAssetTypeName };
            _context.AssetTypes.Add(assetType);
            _context.SaveChanges();
        }

        device.DeviceName = request.DeviceName;
        // Only update SN if the new one is not an AUTO gen (to keep existing auto-gen intact if left blank)
        if (!string.IsNullOrEmpty(request.SerialNumber)) 
        {
            device.SerialNumber = request.SerialNumber;
            device.Imei = request.SerialNumber;
        }
        else if (string.IsNullOrEmpty(device.SerialNumber))
        {
            device.SerialNumber = serialNumber;
            device.Imei = serialNumber;
        }

        device.SiteId = request.SiteId;
        device.AssetTypeId = assetType.Id;
        device.UpsCategory = deviceCategoryType;
        device.IpAddress = request.IpAddress;
        device.UpsUsername = request.UpsUsername;
        device.UpsPassword = request.UpsPassword;
        
        if (targetAssetTypeName == "UPS")
        {
            device.UpsPhaseType = request.UpsPhaseType;
            device.UpsType = request.UpsType;
            device.RoomSensorIp = request.RoomSensorIp;
        }
        else
        {
            // Nullify UPS specific fields to keep clean
            device.UpsPhaseType = null;
            device.UpsType = null;
            device.RoomSensorIp = null;
        }

        device.UpdatedAt = DateTime.UtcNow;

        _context.SaveChanges();

        return Json(new { success = "UPS device berhasil diperbarui." });
    }

    [HttpDelete("/api/ups/manage/devices/{id}")]
    public IActionResult Destroy(string id)
    {
        if (!Guid.TryParse(id, out Guid guidId)) return NotFound();
        var device = _context.Devices.FirstOrDefault(d => d.Id == guidId);
        if (device != null)
        {
            _context.Devices.Remove(device);
            _context.SaveChanges();
        }
        return Json(new { success = "UPS device berhasil dihapus." });
    }
}





