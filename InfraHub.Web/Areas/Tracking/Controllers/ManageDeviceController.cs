using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace InfraHub.Web.Areas.Tracking.Controllers;

[Area("Tracking")]
[Authorize]
[Route("Tracking/Manage/Devices")]
public class ManageDeviceController : Controller
{
    private readonly AppDbContext _context;

    public ManageDeviceController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string type)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            if (type == "unregistered")
            {
                var unregData = await _context.UnregisteredDevices
                    .OrderByDescending(u => u.LastSeen)
                    .Select(u => new {
                        u.Id,
                        u.SerialNumber,
                        u.LastSeen
                    })
                    .ToListAsync();

                var formattedUnreg = unregData.Select((row, index) => {
                    var dict = new Dictionary<string, object>();
                    dict["DT_RowIndex"] = index + 1;
                    dict["serial_number"] = row.SerialNumber;
                    dict["last_seen"] = row.LastSeen.HasValue ? row.LastSeen.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
                    dict["action"] = $"<button type=\"button\" class=\"btn btn-sm btn-primary rounded-pill shadow-sm register-btn\" data-id=\"{row.Id}\" data-sn=\"{row.SerialNumber}\" title=\"Register Device\"><i class=\"fa-solid fa-plus me-1\"></i> Register</button>";
                    return dict;
                });

                return Json(new { draw = 1, recordsTotal = unregData.Count, recordsFiltered = unregData.Count, data = formattedUnreg });
            }
            else
            {
                var devData = await _context.Devices
                    .Include(d => d.AssetType)
                    .Include(d => d.Site)
                    .Include(d => d.Zone)
                    .Where(d => d.AssetType.TypeName == "Tracking")
                    .OrderByDescending(d => d.Id)
                    .Select(d => new {
                        d.Id,
                        d.SerialNumber,
                        d.DeviceName,
                        d.Imei,
                        d.PhoneNumber,
                        SiteName = d.Site != null ? d.Site.SiteName : (d.Zone != null ? d.Zone.ZoneName : "-"),
                        AssetTypeName = d.AssetType != null ? d.AssetType.TypeName : "-",
                        FleetType = d.FleetType ?? "-",
                        IsActive = d.IsActive
                    })
                    .ToListAsync();

                var formattedDev = devData.Select((row, index) => {
                    var dict = new Dictionary<string, object>();
                    dict["DT_RowIndex"] = index + 1;
                    dict["serial_number"] = row.SerialNumber;
                    dict["device_name"] = row.DeviceName;
                    
                    var conn = new List<string>();
                    if (!string.IsNullOrEmpty(row.Imei)) conn.Add($"IMEI: {row.Imei}");
                    if (!string.IsNullOrEmpty(row.PhoneNumber)) conn.Add($"GSM: {row.PhoneNumber}");
                    dict["connectivity"] = conn.Any() ? string.Join("<br>", conn) : "-";
                    
                    dict["site_zone"] = row.SiteName;
                    dict["asset_type"] = row.AssetTypeName;
                    dict["fleet_type"] = row.FleetType;
                    dict["status"] = row.IsActive ? "<span class=\"badge bg-success-subtle text-success px-3 py-2 rounded-pill fw-medium\"><i class=\"fa-solid fa-circle-check me-1\"></i> Active</span>" : "<span class=\"badge bg-danger-subtle text-danger px-3 py-2 rounded-pill fw-medium\"><i class=\"fa-solid fa-circle-xmark me-1\"></i> Inactive</span>";
                    
                    var btn = "<div class=\"d-flex gap-2\">";
                    btn += $"<button type=\"button\" class=\"btn btn-sm btn-light text-primary border-0 shadow-sm edit-device-btn\" data-id=\"{row.Id}\" title=\"Edit Device\"><i class=\"fa-solid fa-pen-to-square\"></i></button>";
                    btn += $"<button type=\"button\" class=\"btn btn-sm btn-light text-danger border-0 shadow-sm delete-device-btn\" data-id=\"{row.Id}\" title=\"Delete Device\"><i class=\"fa-solid fa-trash-can\"></i></button>";
                    btn += "</div>";
                    dict["action"] = btn;
                    return dict;
                });

                return Json(new { draw = 1, recordsTotal = devData.Count, recordsFiltered = devData.Count, data = formattedDev });
            }
        }

        ViewBag.AllSites = await _context.Sites.Select(s => new { id = s.Id, site_code = s.SiteCode, site_name = s.SiteName }).ToListAsync();
        ViewBag.AssetTypes = await _context.AssetTypes
            .Where(a => a.TypeName.Contains("Tracking"))
            .ToDictionaryAsync(a => a.Id.ToString(), a => a.TypeName);

        return View("~/Areas/Tracking/Views/Manage/Devices.cshtml");
    }
}

