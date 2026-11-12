using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ITAssetTrack.Data;
using ITAssetTrack.Models;

namespace ITAssetTrack.Infrastructure.Services
{
    public class ScheduledPmExcelImportResult
    {
        public int TotalProcessed { get; set; }
        public int TotalInserted { get; set; }
        public int TotalUpdated { get; set; }
        public string MonthYear { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> Messages { get; set; } = new();
    }

    public class ScheduledPmExcelImportService
    {
        private readonly AppDbContext _context;

        public ScheduledPmExcelImportService(AppDbContext context)
        {
            _context = context;
            ExcelPackage.License.SetNonCommercialOrganization("InfraHub");
        }

        public async Task<ScheduledPmExcelImportResult> ImportExcelAsync(string filePath, string? customMonthYear = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File Excel Rencana PM '{filePath}' tidak ditemukan.");

            using var stream = File.OpenRead(filePath);
            return await ImportExcelStreamAsync(stream, customMonthYear);
        }

        public async Task<ScheduledPmExcelImportResult> ImportExcelStreamAsync(Stream stream, string? customMonthYear = null)
        {
            var result = new ScheduledPmExcelImportResult();

            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();

            if (worksheet == null || worksheet.Dimension == null)
            {
                result.Messages.Add("Sheet Excel kosong atau tidak valid.");
                return result;
            }

            int rowCount = worksheet.Dimension.Rows;
            if (rowCount < 2)
            {
                result.Messages.Add("File Excel tidak memiliki baris data.");
                return result;
            }

            // ── Dynamic Column Header Auto-Detection ──
            int colSn = -1;
            int colCompName = -1;
            int colNrp = -1;
            int colNamaUser = -1;
            int colDivision = -1;
            int colContact = -1;
            int colEmail = -1;
            int colModel = -1;
            int colStatus = -1;
            int colPlanDate = -1;
            int colActualDate = -1;

            int maxCols = worksheet.Dimension.Columns;
            for (int col = 1; col <= maxCols; col++)
            {
                string headerStr = worksheet.Cells[1, col].Value?.ToString()?.Trim()?.ToLower() ?? string.Empty;
                if (string.IsNullOrEmpty(headerStr)) continue;

                if (colSn == -1 && (headerStr.Contains("service tag") || headerStr.Contains("serial") || headerStr == "sn" || headerStr.Contains("sn ")))
                    colSn = col;
                else if (colCompName == -1 && (headerStr.Contains("comp") || headerStr.Contains("computer") || headerStr.Contains("pc name") || headerStr.Contains("host")))
                    colCompName = col;
                else if (colNrp == -1 && headerStr.Contains("nrp"))
                    colNrp = col;
                else if (colNamaUser == -1 && (headerStr.Contains("nama user") || headerStr.Contains("user") || headerStr.Contains("nama")))
                    colNamaUser = col;
                else if (colDivision == -1 && (headerStr.Contains("divisi") || headerStr.Contains("site") || headerStr.Contains("dept")))
                    colDivision = col;
                else if (colContact == -1 && (headerStr.Contains("kontak") || headerStr.Contains("phone") || headerStr.Contains("hp") || headerStr.Contains("call")))
                    colContact = col;
                else if (colEmail == -1 && headerStr.Contains("email"))
                    colEmail = col;
                else if (colModel == -1 && (headerStr.Contains("tipe detail") || headerStr.Contains("model") || headerStr.Contains("perangkat")))
                    colModel = col;
                else if (colStatus == -1 && headerStr.Contains("status"))
                    colStatus = col;
                else if (colPlanDate == -1 && (headerStr.Contains("tanggal pm") || headerStr.Contains("tanggal rencana") || headerStr.Contains("plan date") || headerStr.Contains("tgl pm") || headerStr.Contains("rencana pm")) && !headerStr.Contains("actual"))
                    colPlanDate = col;
                else if (colActualDate == -1 && (headerStr.Contains("actual") || headerStr.Contains("selesai") || headerStr.Contains("act date")))
                    colActualDate = col;
            }

            // Fallbacks if header auto-detection missed any column
            if (colSn == -1) colSn = 2;
            if (colCompName == -1) colCompName = 3;
            if (colNamaUser == -1) colNamaUser = colNrp != -1 ? colNrp + 1 : 4;
            if (colDivision == -1) colDivision = 5;
            if (colContact == -1) colContact = 6;
            if (colEmail == -1) colEmail = 7;
            if (colModel == -1) colModel = 8;
            if (colStatus == -1) colStatus = 9;
            if (colPlanDate == -1) colPlanDate = 10;
            if (colActualDate == -1) colActualDate = 11;

            // Prioritize customMonthYear if provided, otherwise auto-detect from the Excel dates
            string planMonthStr = customMonthYear ?? string.Empty;
            if (string.IsNullOrEmpty(planMonthStr))
            {
                for (int row = 2; row <= rowCount; row++)
                {
                    var val = worksheet.Cells[row, colPlanDate].Value;
                    if (val != null)
                    {
                        var parsed = ParseExcelDate(val);
                        if (parsed != DateTime.Today)
                        {
                            planMonthStr = parsed.ToString("yyyy-MM");
                            break;
                        }
                    }
                }
                if (string.IsNullOrEmpty(planMonthStr))
                {
                    planMonthStr = DateTime.Today.ToString("yyyy-MM");
                }
            }
            result.MonthYear = planMonthStr;

            int inserted = 0;
            int updated = 0;

            for (int row = 2; row <= rowCount; row++)
            {
                string snRaw = worksheet.Cells[row, colSn].Value?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(snRaw)) continue;

                // Clean Hardware SN if MAC/UUID prefix exists
                string sn = snRaw.Replace("00000000-0000-0000-0000-", "").Trim();
                string compName = colCompName > 0 ? worksheet.Cells[row, colCompName].Value?.ToString()?.Trim() ?? string.Empty : string.Empty;
                string nrp = colNrp > 0 ? worksheet.Cells[row, colNrp].Value?.ToString()?.Trim() ?? string.Empty : string.Empty;
                if (nrp == "#N/A") nrp = string.Empty;

                string namaUser = colNamaUser > 0 ? worksheet.Cells[row, colNamaUser].Value?.ToString()?.Trim() ?? string.Empty : string.Empty;
                string division = colDivision > 0 ? worksheet.Cells[row, colDivision].Value?.ToString()?.Trim() ?? string.Empty : string.Empty;
                string contact = colContact > 0 ? worksheet.Cells[row, colContact].Value?.ToString()?.Trim()?.Replace("callto:", "") ?? string.Empty : string.Empty;
                string email = colEmail > 0 ? worksheet.Cells[row, colEmail].Value?.ToString()?.Trim() ?? string.Empty : string.Empty;
                string modelDetail = colModel > 0 ? worksheet.Cells[row, colModel].Value?.ToString()?.Trim() ?? string.Empty : string.Empty;
                string statusExcel = colStatus > 0 ? worksheet.Cells[row, colStatus].Value?.ToString()?.Trim() ?? "Planned" : "Planned";

                var planDateVal = worksheet.Cells[row, colPlanDate].Value;
                DateTime rawPlanDate = planDateVal != null ? ParseExcelDate(planDateVal) : DateTime.Today;
                DateTime planDate = AlignDateToMonth(rawPlanDate, planMonthStr);

                DateTime? actualDate = null;
                if (colActualDate > 0)
                {
                    var actualVal = worksheet.Cells[row, colActualDate].Value;
                    if (actualVal != null)
                    {
                        var parsedActual = ParseExcelDate(actualVal);
                        if (parsedActual != DateTime.Today) actualDate = parsedActual;
                    }
                }

                // Lookup asset from Master Asset database to prefill brand/specs if available
                var masterAsset = await _context.Assets
                    .Include(a => a.BrandObj)
                    .Include(a => a.DeviceTypeObj)
                    .FirstOrDefaultAsync(a => a.SerialNumber.ToLower() == sn.ToLower());

                // Check if this scheduled item already exists (search by SN across ALL months)
                // This prevents duplicates when re-importing after a wrong-month import
                var existingItem = await _context.ScheduledMaints
                    .FirstOrDefaultAsync(s => s.SerialNumber.ToLower() == sn.ToLower()
                                              && (s.PlanMonth == planMonthStr
                                                  || s.Status == "Planned"
                                                  || s.Status == "In Progress"));

                if (existingItem != null)
                {
                    // Correct PlanMonth if it was previously stored under wrong month
                    existingItem.PlanMonth = planMonthStr;
                    existingItem.PlanDate = planDate;
                    existingItem.Tanggal = planDate;
                    existingItem.NamaUser = string.IsNullOrEmpty(namaUser) ? existingItem.NamaUser : namaUser;
                    existingItem.NRP = string.IsNullOrEmpty(nrp) ? existingItem.NRP : nrp;
                    existingItem.ContactInfo = contact;
                    existingItem.Email = email;
                    existingItem.Division = division;
                    existingItem.ComputerName = string.IsNullOrEmpty(compName) ? existingItem.ComputerName : compName;
                    existingItem.Model = string.IsNullOrEmpty(modelDetail) ? existingItem.Model : modelDetail;
                    existingItem.UpdatedAt = DateTime.Now;

                    if (masterAsset != null)
                    {
                        existingItem.Brand = masterAsset.BrandObj?.Name ?? masterAsset.Brand;
                        existingItem.Ram = masterAsset.Ram;
                        existingItem.Storage = masterAsset.Storage;
                        existingItem.Processor = masterAsset.Processor;
                        existingItem.WindowsVersion = masterAsset.WindowsVersion;
                    }
                    updated++;
                }
                else
                {
                    int docCount = await _context.ScheduledMaints.CountAsync() + 1;
                    var newItem = new ScheduledMaint
                    {
                        Id = Guid.NewGuid(),
                        PlanMonth = planMonthStr,
                        PlanDate = planDate,
                        Tanggal = planDate,
                        ActualDate = actualDate,
                        SerialNumber = sn,
                        ComputerName = compName,
                        NRP = nrp,
                        NamaUser = namaUser,
                        Division = division,
                        ContactInfo = contact,
                        Email = email,
                        Model = modelDetail,
                        NoDocument = $"InfraHub/HQ_DEPT/PM/{planDate.Year}/{docCount:D3}",
                        Status = statusExcel.Contains("Sudah", StringComparison.OrdinalIgnoreCase) || statusExcel.Contains("Complete", StringComparison.OrdinalIgnoreCase) ? "Completed" : "Planned",
                        Brand = masterAsset?.BrandObj?.Name ?? masterAsset?.Brand ?? "Dell",
                        DeviceType = masterAsset?.DeviceTypeObj?.Name ?? masterAsset?.DeviceType ?? "Notebook",
                        Ram = masterAsset?.Ram ?? "16GB",
                        Storage = masterAsset?.Storage ?? "512GB",
                        Processor = masterAsset?.Processor ?? "Intel",
                        WindowsVersion = masterAsset?.WindowsVersion ?? "Win 11",
                        CreatedAt = DateTime.Now
                    };
                    _context.ScheduledMaints.Add(newItem);
                    inserted++;
                }
            }

            // Sync or update PM Plan Target Quantity for this month
            int totalPlanItems = await _context.ScheduledMaints.CountAsync(s => s.PlanMonth == planMonthStr) + inserted;
            var pmPlan = await _context.PmPlans.FirstOrDefaultAsync(p => p.MonthYear == planMonthStr);
            if (pmPlan == null)
            {
                pmPlan = new PmPlan
                {
                    Id = Guid.NewGuid(),
                    MonthYear = planMonthStr,
                    TargetQuantity = totalPlanItems,
                    CreatedAt = DateTime.Now
                };
                _context.PmPlans.Add(pmPlan);
            }
            else
            {
                pmPlan.TargetQuantity = totalPlanItems;
                pmPlan.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            result.TotalProcessed = inserted + updated;
            result.TotalInserted = inserted;
            result.TotalUpdated = updated;
            result.Messages.Add($"Berhasil memproses {result.TotalProcessed} target PM untuk Bulan {planMonthStr} ({inserted} baru, {updated} diperbarui). Target PM Plan diperbarui menjadi {totalPlanItems} unit.");

            return result;
        }

        /// <summary>
        /// Align a parsed date to the selected target plan month.
        /// </summary>
        private static DateTime AlignDateToMonth(DateTime parsedDate, string targetMonthYear)
        {
            if (string.IsNullOrEmpty(targetMonthYear) || !targetMonthYear.Contains("-"))
                return parsedDate;

            try
            {
                var parts = targetMonthYear.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out int year) && int.TryParse(parts[1], out int month))
                {
                    int day = parsedDate.Day;
                    int daysInMonth = DateTime.DaysInMonth(year, month);
                    if (day > daysInMonth) day = daysInMonth;

                    return new DateTime(year, month, day, parsedDate.Hour, parsedDate.Minute, parsedDate.Second);
                }
            }
            catch
            {
                // Fallback to parsed date
            }
            return parsedDate;
        }

        /// <summary>
        /// Parse a cell value from EPPlus into DateTime.
        /// EPPlus may return dates as DateTime, double (OADate serial), or string.
        /// </summary>
        private static DateTime ParseExcelDate(object? cellValue)
        {
            if (cellValue == null) return DateTime.Today;

            // EPPlus returns actual DateTime when cell is formatted as date
            if (cellValue is DateTime dt) return dt;

            // EPPlus sometimes returns date serial as double (OADate)
            if (cellValue is double d && d > 1)
            {
                try { return DateTime.FromOADate(d); }
                catch { /* fall through */ }
            }

            // Try parsing string with common Indonesian/Excel formats
            string strVal = cellValue.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(strVal)) return DateTime.Today;

            // Check if string is a numeric OA Date
            if (double.TryParse(strVal, out double dVal) && dVal > 29000 && dVal < 60000)
            {
                try { return DateTime.FromOADate(dVal); }
                catch { /* fall through */ }
            }

            string[] formats = {
                "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
                "MM/dd/yyyy", "yyyy-MM-dd", "yyyy/MM/dd",
                "dd MMM yyyy", "d MMM yyyy", "dd MMMM yyyy",
                "dd-MMM-yyyy", "d-MMM-yyyy", "dd-MMMM-yyyy",
                "dd/MMM/yyyy", "d/MMM/yyyy"
            };

            if (DateTime.TryParseExact(strVal, formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
                return parsed;

            // Try parsing using English/Invariant culture explicitly
            if (DateTime.TryParse(strVal, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var invParsed))
                return invParsed;

            // Try parsing using Indonesian culture explicitly
            if (DateTime.TryParse(strVal, new System.Globalization.CultureInfo("id-ID"),
                System.Globalization.DateTimeStyles.None, out var idParsed))
                return idParsed;

            // Last resort
            if (DateTime.TryParse(strVal, out var fallback)) return fallback;

            return DateTime.Today;
        }
    }
}
