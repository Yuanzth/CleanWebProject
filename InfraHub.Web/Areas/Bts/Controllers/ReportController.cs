using InfraHub.Infrastructure.Data;
using InfraHub.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InfraHub.Web.Areas.Bts.Controllers;

[Area("Bts")]
[Authorize]
public class ReportController : Controller
{
    private readonly AppDbContext _context;
    private readonly ReportService _reportService;

    public ReportController(AppDbContext context, ReportService reportService)
    {
        _context = context;
        _reportService = reportService;
    }

    [HttpGet("Bts/Report")]
    public async Task<IActionResult> Index(string area_id, string date_from, string date_to)
    {
        ViewBag.Areas = await _context.Sites.ToListAsync();
        ViewBag.AreaId = area_id;
        ViewBag.DateFrom = date_from ?? DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
        ViewBag.DateTo = date_to ?? DateTime.Now.ToString("yyyy-MM-dd");

        if (!string.IsNullOrEmpty(area_id) && Guid.TryParse(area_id, out var parsedAreaId))
        {
            var reportData = await _reportService.GetReportSummaryAsync(
                parsedAreaId, 
                DateTime.Parse(ViewBag.DateFrom), 
                DateTime.Parse(ViewBag.DateTo));

            var reportJson = JsonSerializer.Serialize(reportData);
            ViewBag.ReportJson = reportJson;
            ViewBag.Report = System.Text.Json.Nodes.JsonNode.Parse(reportJson);
        }
        
        return View();
    }

    [HttpGet("Bts/Report/Summary")]
    public async Task<IActionResult> GetSummary(string area_id, string date_from, string date_to)
    {
        if (string.IsNullOrEmpty(area_id))
            return BadRequest(new { status = "error", message = "Site wajib dipilih." });

        if (!Guid.TryParse(area_id, out var parsedAreaId))
            return BadRequest(new { status = "error", message = "Format Site tidak valid." });

        try
        {
            var dateFrom = DateTime.Parse(date_from ?? DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd"));
            var dateTo = DateTime.Parse(date_to ?? DateTime.Now.ToString("yyyy-MM-dd"));

            var reportData = await _reportService.GetReportSummaryAsync(parsedAreaId, dateFrom, dateTo);

            return Json(new { 
                status = "success", 
                data = reportData,
                meta = new {
                    area_id = area_id,
                    date_from = dateFrom.ToString("yyyy-MM-dd"),
                    date_to = dateTo.ToString("yyyy-MM-dd")
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpGet("Bts/Report/ExportLog")]
    public IActionResult ExportLog()
    {
        // TODO: Implement ClosedXML / EPPlus for Excel Exporting
        return Content("Export function under development...");
    }
}
