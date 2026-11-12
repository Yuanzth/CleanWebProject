using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITAssetTrack.Data;

namespace ITAssetTrack.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TelemetryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TelemetryController(AppDbContext context)
        {
            _context = context;
        }

        public class TelemetryPayload
        {
            public string SerialNumber { get; set; } = string.Empty;
            public bool IsAFK { get; set; }
            public string Uptime { get; set; } = string.Empty;
            public string ActiveApps { get; set; } = string.Empty;
        }

        [HttpPost("CheckIn")]
        public async Task<IActionResult> CheckIn([FromBody] TelemetryPayload payload)
        {
            if (string.IsNullOrEmpty(payload.SerialNumber))
                return BadRequest("Serial Number is required");

            var asset = await _context.Assets.FirstOrDefaultAsync(a => a.SerialNumber == payload.SerialNumber);
            if (asset == null)
                return NotFound($"Asset with SN {payload.SerialNumber} not found");

            asset.LastOnlineTime = DateTime.Now;
            asset.IsAFK = payload.IsAFK;
            asset.Uptime = payload.Uptime;
            asset.ActiveApps = payload.ActiveApps;
            asset.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Telemetry updated successfully" });
        }
    }
}
