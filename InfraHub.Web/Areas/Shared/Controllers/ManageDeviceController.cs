using InfraHub.Infrastructure.Data;
using InfraHub.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InfraHub.Web.Areas.Shared.Controllers;

[Area("Shared")]
[Authorize]
public class ManageDeviceController : Controller
{
    private readonly AppDbContext _context;

    public ManageDeviceController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("Shared/ManageDevices")]
    public IActionResult Index()
    {
        // Only Administrator can access this view
        if (User.FindFirst("RoleName")?.Value != "Administrator")
            return Forbid();

        ViewBag.AssetTypes = _context.AssetTypes.ToList();
        ViewBag.AllZones = _context.Zones.Include(z => z.Site).ToList();
        return View();
    }

    [HttpGet("Shared/ManageDevices/GetData")]
    public async Task<IActionResult> GetData(string type)
    {
        if (type == "registered")
        {
            var data = await _context.Devices
                .Include(d => d.Zone)
                    .ThenInclude(z => z.Site)
                .Include(d => d.AssetType)
                .OrderByDescending(x => x.Id)
                .Select(d => new {
                    d.Id,
                    d.SerialNumber,
                    d.DeviceName,
                    d.Imei,
                    d.PhoneNumber,
                    SiteCode = d.Zone != null && d.Zone.Site != null ? d.Zone.Site.SiteCode : "-",
                    ZoneName = d.Zone != null ? d.Zone.ZoneName : "-",
                    AssetTypeName = d.AssetType != null ? d.AssetType.TypeName : "-",
                    NetworkArea = d.NetworkArea,
                    d.IsActive
                })
                .ToListAsync();

            return Json(new { data = data });
        }

        if (type == "unregistered")
        {
            var rawData = await _context.UnregisteredDevices
                .OrderByDescending(x => x.LastSeen)
                .Select(d => new {
                    d.Id,
                    d.SerialNumber,
                    d.LastSeen
                })
                .ToListAsync();

            var data = rawData.Select(d => new {
                d.Id,
                d.SerialNumber,
                LastSeen = d.LastSeen.HasValue ? d.LastSeen.Value.ToString("dd/MM/yyyy HH:mm:ss") : "-"
            });

            return Json(new { data = data });
        }

        return BadRequest("Invalid type");
    }

    // CRUD Endpoints for Device
    [HttpGet("Shared/ManageDevices/Device/{id}")]
    public async Task<IActionResult> GetDevice(Guid id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null) return NotFound();
        return Json(device);
    }

    [HttpPost("Shared/ManageDevices/Register")]
    public async Task<IActionResult> RegisterDevice([FromForm] Device device, [FromForm] Guid unregistered_id)
    {
        // Add to Devices
        device.CreatedAt = DateTime.UtcNow;
        device.UpdatedAt = DateTime.UtcNow;
        _context.Devices.Add(device);

        // Delete from Unregistered
        var unreg = await _context.UnregisteredDevices.FindAsync(unregistered_id);
        if (unreg != null)
        {
            _context.UnregisteredDevices.Remove(unreg);
        }

        await _context.SaveChangesAsync();
        return Json(new { success = "Device registered successfully" });
    }

    [HttpPost("Shared/ManageDevices/BatchRegister")]
    public async Task<IActionResult> BatchRegisterDevices([FromBody] List<BatchRegisterItemDto> items)
    {
        if (items == null || !items.Any())
            return BadRequest(new { message = "No devices provided for registration." });

        int registeredCount = 0;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.SerialNumber) || string.IsNullOrWhiteSpace(item.DeviceName))
                continue;

            var device = new Device
            {
                Id = Guid.NewGuid(),
                SerialNumber = item.SerialNumber.Trim(),
                DeviceName = item.DeviceName.Trim(),
                Imei = string.IsNullOrWhiteSpace(item.Imei) ? null : item.Imei.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(item.PhoneNumber) ? null : item.PhoneNumber.Trim(),
                SiteId = item.SiteId.HasValue && item.SiteId.Value != Guid.Empty ? item.SiteId.Value : null,
                AssetTypeId = (item.AssetTypeId.HasValue && item.AssetTypeId.Value != Guid.Empty) ? item.AssetTypeId.Value : Guid.Empty,
                FleetType = string.IsNullOrWhiteSpace(item.FleetType) ? null : item.FleetType.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Devices.Add(device);
            registeredCount++;

            if (item.UnregisteredId.HasValue && item.UnregisteredId.Value != Guid.Empty)
            {
                var unreg = await _context.UnregisteredDevices.FindAsync(item.UnregisteredId.Value);
                if (unreg != null)
                {
                    _context.UnregisteredDevices.Remove(unreg);
                }
            }
            else
            {
                var snClean = item.SerialNumber.Trim().ToLower();
                var unregBySn = await _context.UnregisteredDevices
                    .FirstOrDefaultAsync(u => u.SerialNumber.ToLower() == snClean);
                if (unregBySn != null)
                {
                    _context.UnregisteredDevices.Remove(unregBySn);
                }
            }
        }

        await _context.SaveChangesAsync();
        return Json(new { success = $"Successfully registered {registeredCount} device(s)!" });
    }

    [HttpPut("Shared/ManageDevices/Device/{id}")]
    public async Task<IActionResult> UpdateDevice(Guid id, [FromForm] Device deviceUpdate)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null) return NotFound(new { error = "Device not found" });

        device.SerialNumber = deviceUpdate.SerialNumber;
        device.Imei = deviceUpdate.Imei;
        device.PhoneNumber = deviceUpdate.PhoneNumber;
        device.DeviceName = deviceUpdate.DeviceName;
        device.AssetHeight = deviceUpdate.AssetHeight;
        device.AssetTypeId = deviceUpdate.AssetTypeId;
        device.ZoneId = deviceUpdate.ZoneId;
        device.IsActive = deviceUpdate.IsActive;
        device.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Json(new { success = "Device updated successfully" });
    }

    [HttpDelete("Shared/ManageDevices/Device/{id}")]
    public async Task<IActionResult> DeleteDevice(Guid id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null) return NotFound(new { error = "Device not found" });

        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();
        return Json(new { success = "Device deleted successfully" });
    }

    [HttpDelete("Shared/ManageDevices/Unregistered/{id}")]
    public async Task<IActionResult> DeleteUnregistered(Guid id)
    {
        var unreg = await _context.UnregisteredDevices.FindAsync(id);
        if (unreg == null) return NotFound(new { error = "Unregistered device not found" });

        _context.UnregisteredDevices.Remove(unreg);
        await _context.SaveChangesAsync();
        return Json(new { success = "Unregistered device deleted successfully" });
    }
}

public class BatchRegisterItemDto
{
    public Guid? UnregisteredId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? Imei { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? AssetTypeId { get; set; }
    public string FleetType { get; set; } = string.Empty;
}
