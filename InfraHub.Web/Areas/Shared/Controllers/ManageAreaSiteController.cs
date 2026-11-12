using InfraHub.Infrastructure.Data;
using InfraHub.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InfraHub.Web.Areas.Shared.Controllers;

[Area("Shared")]
[Authorize]
public class ManageAreaSiteController : Controller
{
    private readonly AppDbContext _context;

    public ManageAreaSiteController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        // Only Administrator can access this view
        if (User.FindFirst("RoleName")?.Value != "Administrator")
            return Forbid();

        ViewBag.Sites = _context.Sites.ToList();
        ViewBag.Zones = _context.Zones.ToList();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetData(string type)
    {
        if (type == "site")
        {
            var data = await _context.Sites
                .OrderByDescending(x => x.Id)
                .Select(s => new {
                    s.Id,
                    s.SiteCode,
                    s.SiteName,
                    s.Latitude,
                    s.Longitude
                })
                .ToListAsync();
            return Json(new { data = data });
        }
        
        if (type == "zone")
        {
            var data = await _context.Zones
                .OrderByDescending(x => x.Id)
                .ToListAsync();
            return Json(new { data = data });
        }

        return BadRequest("Invalid type");
    }

    [HttpGet("Shared/ManageAreaSite/Site/{id}")]
    public async Task<IActionResult> GetSite(Guid id)
    {
        var site = await _context.Sites.FindAsync(id);
        if (site == null) return NotFound();
        return Json(site);
    }

    [HttpPost("Shared/ManageAreaSite/Site")]
    public async Task<IActionResult> CreateSite([FromForm] Site site)
    {
        if (site.Id == Guid.Empty)
        {
            site.Id = Guid.NewGuid();
        }

        // Manually parse Latitude and Longitude using InvariantCulture to fix culture format issues
        if (Request.Form.TryGetValue("Latitude", out var latStr) && !string.IsNullOrEmpty(latStr))
        {
            if (decimal.TryParse(latStr.ToString().Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat))
                site.Latitude = lat;
        }
        if (Request.Form.TryGetValue("Longitude", out var lngStr) && !string.IsNullOrEmpty(lngStr))
        {
            if (decimal.TryParse(lngStr.ToString().Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng))
                site.Longitude = lng;
        }

        // We clear validation errors for navigation properties and Id since they are not submitted from the form.
        ModelState.Remove(nameof(site.Id));
        ModelState.Remove(nameof(site.Zones));
        ModelState.Remove(nameof(site.Devices));
        ModelState.Remove(nameof(site.UserAssignments));
        ModelState.Remove(nameof(site.SiteBasemaps));
        ModelState.Remove(nameof(site.SiteBoundaries));
        ModelState.Remove(nameof(site.SiteCoverages));
        ModelState.Remove(nameof(site.Latitude)); // Remove in case binder failed parsing
        ModelState.Remove(nameof(site.Longitude));

        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "Please check your form input." });
        }

        _context.Sites.Add(site);
        await _context.SaveChangesAsync();
        return Json(new { success = "Site added successfully" });
    }

    [HttpPut("Shared/ManageAreaSite/Site/{id}")]
    public async Task<IActionResult> UpdateSite(Guid id, [FromForm] Site siteUpdate)
    {
        var site = await _context.Sites.FindAsync(id);
        if (site == null) return NotFound(new { error = "Site not found" });

        site.SiteCode = siteUpdate.SiteCode;
        site.SiteName = siteUpdate.SiteName;
        
        // Manually parse Latitude and Longitude using InvariantCulture
        if (Request.Form.TryGetValue("Latitude", out var latStr) && !string.IsNullOrEmpty(latStr))
        {
            if (decimal.TryParse(latStr.ToString().Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat))
                site.Latitude = lat;
            else
                site.Latitude = null;
        }
        else { site.Latitude = null; }
        
        if (Request.Form.TryGetValue("Longitude", out var lngStr) && !string.IsNullOrEmpty(lngStr))
        {
            if (decimal.TryParse(lngStr.ToString().Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng))
                site.Longitude = lng;
            else
                site.Longitude = null;
        }
        else { site.Longitude = null; }

        await _context.SaveChangesAsync();
        return Json(new { success = "Site updated successfully" });
    }

    [HttpDelete("Shared/ManageAreaSite/Site/{id}")]
    public async Task<IActionResult> DeleteSite(Guid id)
    {
        var site = await _context.Sites.FindAsync(id);
        if (site == null) return NotFound(new { error = "Site not found" });

        _context.Sites.Remove(site);
        await _context.SaveChangesAsync();
        return Json(new { success = "Site deleted successfully" });
    }

    // Zone Endpoints
    [HttpGet("Shared/ManageAreaSite/Zone/{id}")]
    public async Task<IActionResult> GetZone(Guid id)
    {
        var zone = await _context.Zones.FindAsync(id);
        if (zone == null) return NotFound();
        return Json(zone);
    }

    [HttpPost("Shared/ManageAreaSite/Zone")]
    public async Task<IActionResult> CreateZone([FromForm] Zone zone)
    {
        if (zone.Id == Guid.Empty)
        {
            zone.Id = Guid.NewGuid();
        }

        ModelState.Remove(nameof(zone.Id));
        ModelState.Remove(nameof(zone.Site));
        ModelState.Remove(nameof(zone.Devices));

        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "Please check your form input." });
        }

        zone.CreatedAt = DateTime.UtcNow;
        zone.UpdatedAt = DateTime.UtcNow;
        _context.Zones.Add(zone);
        await _context.SaveChangesAsync();
        return Json(new { success = "Zone added successfully" });
    }

    [HttpPut("Shared/ManageAreaSite/Zone/{id}")]
    public async Task<IActionResult> UpdateZone(Guid id, [FromForm] Zone zoneUpdate)
    {
        var zone = await _context.Zones.FindAsync(id);
        if (zone == null) return NotFound(new { error = "Zone not found" });

        zone.SiteId = zoneUpdate.SiteId;
        zone.ZoneCode = zoneUpdate.ZoneCode;
        zone.ZoneName = zoneUpdate.ZoneName;
        zone.Description = zoneUpdate.Description;
        zone.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Json(new { success = "Zone updated successfully" });
    }

    [HttpDelete("Shared/ManageAreaSite/Zone/{id}")]
    public async Task<IActionResult> DeleteZone(Guid id)
    {
        var zone = await _context.Zones.FindAsync(id);
        if (zone == null) return NotFound(new { error = "Zone not found" });

        _context.Zones.Remove(zone);
        await _context.SaveChangesAsync();
        return Json(new { success = "Zone deleted successfully" });
    }
}
