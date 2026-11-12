using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace InfraHub.Web.Areas.Tracking.Controllers;

[Area("Tracking")]
[Authorize]
[Route("Tracking/Manage/Sites")]
public class ManageSiteController : Controller
{
    private readonly AppDbContext _context;

    public ManageSiteController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            var data = await _context.Sites
                .Include(s => s.SiteBoundaries)
                .Include(s => s.SiteBasemaps)
                .Include(s => s.SiteCoverages)
                .OrderByDescending(s => s.Id)
                .Select(s => new {
                    id = s.Id,
                    site_code = s.SiteCode,
                    site_name = s.SiteName,
                    status_boundary = s.SiteBoundaries.Any() ? "<span class=\"badge bg-success\">Sudah Ada</span>" : "<span class=\"badge bg-secondary\">Belum Ada</span>",
                    status_basemap = s.SiteBasemaps.Any() ? "<span class=\"badge bg-success\">Sudah Ada</span>" : "<span class=\"badge bg-secondary\">Belum Ada</span>",
                    status_coverage = s.SiteCoverages.Any() ? "<span class=\"badge bg-success\">Sudah Ada</span>" : "<span class=\"badge bg-secondary\">Belum Ada</span>",
                    boundary_id = s.SiteBoundaries.FirstOrDefault() != null ? s.SiteBoundaries.FirstOrDefault().Id : (Guid?)null,
                    basemap_id = s.SiteBasemaps.FirstOrDefault() != null ? s.SiteBasemaps.FirstOrDefault().Id : (Guid?)null,
                    coverage_id = s.SiteCoverages.FirstOrDefault() != null ? s.SiteCoverages.FirstOrDefault().Id : (Guid?)null,
                    boundary_path = s.SiteBoundaries.FirstOrDefault() != null ? s.SiteBoundaries.FirstOrDefault().FilePath : null,
                    basemap_path = s.SiteBasemaps.FirstOrDefault() != null ? s.SiteBasemaps.FirstOrDefault().FilePath : null,
                    coverage_path = s.SiteCoverages.FirstOrDefault() != null ? s.SiteCoverages.FirstOrDefault().FilePath : null
                })
                .ToListAsync();

            // Add index and actions
            var formattedData = data.Select((row, index) => {
                var btn = "<div class=\"d-flex gap-1\">";
                
                // Kelola Peta Action
                btn += $"<button type=\"button\" class=\"btn btn-sm btn-primary kelola-peta-btn\" " +
                       $"data-id=\"{row.id}\" data-name=\"{row.site_name}\" " +
                       $"data-boundary-path=\"{row.boundary_path}\" " +
                       $"data-boundary-id=\"{row.boundary_id}\" " +
                       $"data-basemap-path=\"{row.basemap_path}\" " +
                       $"data-basemap-id=\"{row.basemap_id}\" " +
                       $"data-coverage-path=\"{row.coverage_path}\" " +
                       $"data-coverage-id=\"{row.coverage_id}\" " +
                       $"title=\"Kelola Peta\"><i class=\"fa-solid fa-map-location-dot\"></i> Kelola Peta</button>";

                btn += "</div>";

                var dict = new Dictionary<string, object>();
                dict["DT_RowIndex"] = index + 1;
                dict["site_code"] = row.site_code;
                dict["site_name"] = row.site_name;
                dict["status_boundary"] = row.status_boundary;
                dict["status_basemap"] = row.status_basemap;
                dict["status_coverage"] = row.status_coverage;
                dict["action"] = btn;
                return dict;
            });

            return Json(new { draw = 1, recordsTotal = data.Count, recordsFiltered = data.Count, data = formattedData });
        }

        return View("~/Areas/Tracking/Views/Manage/Sites.cshtml");
    }

    [HttpPost("{siteId}/UpdateMapFiles")]
    [RequestSizeLimit(314572800)] // 300 MB limit total
    [RequestFormLimits(MultipartBodyLengthLimit = 314572800)]
    public async Task<IActionResult> UpdateMapFiles(Guid siteId, IFormFile? boundary_file, IFormFile? basemap_file, IFormFile? coverage_file, [FromForm] string? provider_name)
    {
        if (boundary_file == null && basemap_file == null && coverage_file == null) 
            return BadRequest(new { success = false, message = "Silakan pilih setidaknya satu file untuk diunggah." });

        try
        {
            var site = await _context.Sites.FindAsync(siteId);
            if (site == null) return NotFound(new { success = false, message = "Site not found." });

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 1. Process Boundary
            if (boundary_file != null && boundary_file.Length > 0)
            {
                if (boundary_file.Length > 10485760) return BadRequest(new { success = false, message = "Ukuran file boundary maksimum 10MB." });
                var ext = Path.GetExtension(boundary_file.FileName).ToLower();
                if (ext != ".kmz" && ext != ".kml" && ext != ".zip") return BadRequest(new { success = false, message = "Format boundary invalid." });

                string kmlContent = null;
                using (var ms = new System.IO.MemoryStream())
                {
                    await boundary_file.CopyToAsync(ms);
                    ms.Position = 0;
                    if (ext == ".kmz" || ext == ".zip")
                    {
                        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read))
                        {
                            var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".kml", StringComparison.OrdinalIgnoreCase));
                            if (entry != null)
                            {
                                using (var entryStream = entry.Open())
                                using (var reader = new System.IO.StreamReader(entryStream))
                                    kmlContent = await reader.ReadToEndAsync();
                            }
                        }
                    }
                    else if (ext == ".kml")
                    {
                        using (var reader = new System.IO.StreamReader(ms))
                            kmlContent = await reader.ReadToEndAsync();
                    }
                }

                if (!string.IsNullOrEmpty(kmlContent))
                {
                    var polygonData = ExtractCoordinatesFromKml(kmlContent);
                    if (polygonData != null && polygonData.Any())
                    {
                        var fileName = $"boundary_{site.SiteCode}_{timestamp}{ext}";
                        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "storage", "boundaries");
                        Directory.CreateDirectory(uploadPath);
                        using (var stream = new FileStream(Path.Combine(uploadPath, fileName), FileMode.Create))
                        using (var readStream = boundary_file.OpenReadStream())
                            await readStream.CopyToAsync(stream);

                        var relativePath = $"storage/boundaries/{fileName}";
                        var boundary = await _context.SiteBoundaries.FirstOrDefaultAsync(b => b.SiteId == siteId);
                        if (boundary == null)
                        {
                            _context.SiteBoundaries.Add(new InfraHub.Infrastructure.Data.Entities.SiteBoundary {
                                Id = Guid.NewGuid(), SiteId = siteId, FilePath = relativePath, PolygonData = System.Text.Json.JsonSerializer.Serialize(polygonData),
                                IsActive = true, ProviderName = "Uploaded by User", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            boundary.FilePath = relativePath;
                            boundary.PolygonData = System.Text.Json.JsonSerializer.Serialize(polygonData);
                            boundary.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
            }

            // 2. Process Basemap
            if (basemap_file != null && basemap_file.Length > 0)
            {
                if (basemap_file.Length > 104857600) return BadRequest(new { success = false, message = "Ukuran file basemap maksimum 100MB." });
                var ext = Path.GetExtension(basemap_file.FileName).ToLower();
                if (ext == ".kmz" || ext == ".zip")
                {
                    var fileName = $"basemap_{site.SiteCode}_{timestamp}{ext}";
                    var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "storage", "basemaps");
                    Directory.CreateDirectory(uploadPath);
                    using (var stream = new FileStream(Path.Combine(uploadPath, fileName), FileMode.Create))
                        await basemap_file.CopyToAsync(stream);

                    var relativePath = $"storage/basemaps/{fileName}";
                    var basemap = await _context.SiteBasemaps.FirstOrDefaultAsync(b => b.SiteId == siteId);
                    if (basemap == null)
                    {
                        _context.SiteBasemaps.Add(new InfraHub.Infrastructure.Data.Entities.SiteBasemap {
                            Id = Guid.NewGuid(), SiteId = siteId, FilePath = relativePath, IsActive = true, ProviderName = "Uploaded by User", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        basemap.FilePath = relativePath; basemap.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            // 3. Process Coverage
            if (coverage_file != null && coverage_file.Length > 0)
            {
                if (coverage_file.Length > 104857600) return BadRequest(new { success = false, message = "Ukuran file coverage maksimum 100MB." });
                var ext = Path.GetExtension(coverage_file.FileName).ToLower();
                if (ext == ".kmz" || ext == ".zip")
                {
                    var fileName = $"coverage_{site.SiteCode}_{timestamp}{ext}";
                    var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "storage", "coverages");
                    Directory.CreateDirectory(uploadPath);
                    using (var stream = new FileStream(Path.Combine(uploadPath, fileName), FileMode.Create))
                        await coverage_file.CopyToAsync(stream);

                    var relativePath = $"storage/coverages/{fileName}";
                    var coverage = await _context.SiteCoverages.FirstOrDefaultAsync(b => b.SiteId == siteId);
                    if (coverage == null)
                    {
                        _context.SiteCoverages.Add(new InfraHub.Infrastructure.Data.Entities.SiteCoverage {
                            Id = Guid.NewGuid(), SiteId = siteId, FilePath = relativePath, IsActive = true,
                            ProviderName = string.IsNullOrEmpty(provider_name) ? "Uploaded by User" : provider_name,
                            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        coverage.FilePath = relativePath;
                        if (!string.IsNullOrEmpty(provider_name)) coverage.ProviderName = provider_name;
                        coverage.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Peta berhasil diperbarui." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = "Error: " + ex.Message });
        }
    }

    [HttpDelete("Kmz/{id}")]
    public async Task<IActionResult> DestroyBoundaryKmz(Guid id)
    {
        var boundary = await _context.SiteBoundaries.FindAsync(id);
        if (boundary != null)
        {
            if (!string.IsNullOrEmpty(boundary.FilePath))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", boundary.FilePath);
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            }
            _context.SiteBoundaries.Remove(boundary);
            await _context.SaveChangesAsync();
        }
        return Json(new { success = "Boundary berhasil dihapus." });
    }

    [HttpDelete("Basemap/{id}")]
    public async Task<IActionResult> DestroyBasemapKmz(Guid id)
    {
        var basemap = await _context.SiteBasemaps.FindAsync(id);
        if (basemap != null)
        {
            if (!string.IsNullOrEmpty(basemap.FilePath))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", basemap.FilePath);
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            }
            _context.SiteBasemaps.Remove(basemap);
            await _context.SaveChangesAsync();
        }
        return Json(new { success = "Basemap berhasil dihapus." });
    }

    [HttpDelete("Coverage/{id}")]
    public async Task<IActionResult> DestroyCoverageKmz(Guid id)
    {
        var coverage = await _context.SiteCoverages.FindAsync(id);
        if (coverage != null)
        {
            if (!string.IsNullOrEmpty(coverage.FilePath))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", coverage.FilePath);
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            }
            _context.SiteCoverages.Remove(coverage);
            await _context.SaveChangesAsync();
        }
        return Json(new { success = "Coverage plan berhasil dihapus." });
    }

    private List<double[]> ExtractCoordinatesFromKml(string kmlContent)
    {
        var polygon = new List<double[]>();
        try
        {
            var xDoc = System.Xml.Linq.XDocument.Parse(kmlContent);
            var coordsElement = xDoc.Descendants().FirstOrDefault(d => d.Name.LocalName == "coordinates");
            if (coordsElement != null)
            {
                var coordsString = coordsElement.Value.Trim();
                var points = coordsString.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var point in points)
                {
                    var parts = point.Split(',');
                    if (parts.Length >= 2)
                    {
                        if (double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon) &&
                            double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat))
                        {
                            polygon.Add(new double[] { lat, lon });
                        }
                    }
                }
            }
        }
        catch { }
        return polygon;
    }
}

