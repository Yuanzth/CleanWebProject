using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ITAssetTrack.Data;
using ITAssetTrack.Models;

namespace ITAssetTrack.Services
{
    public class AssetExcelImportResult
    {
        public int TotalRows { get; set; }
        public int ImportedAssets { get; set; }
        public int CreatedEmployees { get; set; }
        public int ActiveAssignments { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
    }

    public class AssetExcelImportService
    {
        private readonly AppDbContext _context;

        public AssetExcelImportService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AssetExcelImportResult> ImportExcelAsync(string filePath)
        {
            ExcelPackage.License.SetNonCommercialOrganization("InfraHub");
            var result = new AssetExcelImportResult();

            if (!File.Exists(filePath))
            {
                result.Messages.Add($"File excel tidak ditemukan di path: {filePath}");
                return result;
            }

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await ImportExcelStreamAsync(stream);
        }

        public async Task<AssetExcelImportResult> ImportExcelStreamAsync(Stream stream)
        {
            ExcelPackage.License.SetNonCommercialOrganization("InfraHub");
            var result = new AssetExcelImportResult();

            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                result.Messages.Add("Sheet excel tidak ditemukan.");
                return result;
            }

            int rowCount = worksheet.Dimension.Rows;
            result.TotalRows = rowCount - 1; // Exclude header

            // Pre-load existing master caches to avoid duplicate DB queries
            var siteDict = await _context.Sites.ToDictionaryAsync(s => s.Code.ToUpper(), s => s);
            var deptDict = await _context.Departments.ToDictionaryAsync(d => d.Name.ToUpper(), d => d);
            var divDict = await _context.Divisions.ToDictionaryAsync(d => d.Code.ToUpper(), d => d);
            var typeDict = await _context.DeviceTypes.ToDictionaryAsync(t => t.Name.ToUpper(), t => t);
            var catDict = await _context.DeviceCategories.ToDictionaryAsync(c => c.Name.ToUpper(), c => c);
            var brandDict = await _context.Brands.ToDictionaryAsync(b => b.Name.ToUpper(), b => b);
            var vendorDict = await _context.Vendors.ToDictionaryAsync(v => v.Name.ToUpper(), v => v);
            var empDict = await _context.Employees.ToDictionaryAsync(e => e.FullName.ToUpper(), e => e);
            var assetDict = await _context.Assets.ToDictionaryAsync(a => a.SerialNumber.ToUpper(), a => a);

            // Dynamic Header Column Mapping
            int colAssetTag = 1, colDistrict = 2, colType = 3, colHostName = 4, colSN = 5;
            int colAssignedTo = 7, colNRP = 8, colDivision = 14, colDepartment = 13, colVendor = 9;
            int colState = 10, colBrand = 11, colModel = 12, colOS = 15, colWinVer = 16;
            int colCategory = 17, colRam = 18, colStorage = 19, colProcessor = 20, colGpu = 21, colRemarks = 22;

            int totalCols = worksheet.Dimension?.Columns ?? 25;
            for (int c = 1; c <= totalCols; c++)
            {
                string header = worksheet.Cells[1, c].Text?.Trim().ToLowerInvariant() ?? "";
                if (header == "asset tag" || header == "asset_tag" || header == "assettag") colAssetTag = c;
                else if (header == "district" || header == "site") colDistrict = c;
                else if (header == "type" || header == "devicetype") colType = c;
                else if (header == "host name" || header == "hostname" || header == "computer name" || header == "computername") colHostName = c;
                else if (header == "serial number" || header == "serial_number" || header == "serialnumber" || header == "sn") colSN = c;
                else if (header == "assigned to" || header == "assignedto" || header == "user" || header == "nama user") colAssignedTo = c;
                else if (header == "nrp") colNRP = c;
                else if (header == "div." || header == "div" || header == "division") colDivision = c;
                else if (header == "dept." || header == "dept" || header == "department") colDepartment = c;
                else if (header == "vendor") colVendor = c;
                else if (header == "state" || header == "status") colState = c;
                else if (header == "brand") colBrand = c;
                else if (header == "model") colModel = c;
                else if (header == "os" || header == "operating system") colOS = c;
                else if (header == "winver" || header == "windows version") colWinVer = c;
                else if (header == "category" || header == "device category") colCategory = c;
                else if (header == "ram") colRam = c;
                else if (header == "storage" || header == "ssd" || header == "hdd") colStorage = c;
                else if (header == "processor" || header == "cpu") colProcessor = c;
                else if (header == "gpu" || header == "vga") colGpu = c;
                else if (header == "remarks" || header == "comments" || header == "remark") colRemarks = c;
            }

            for (int r = 2; r <= rowCount; r++)
            {
                string assetTag = worksheet.Cells[r, colAssetTag].Text?.Trim() ?? string.Empty;
                string district = worksheet.Cells[r, colDistrict].Text?.Trim() ?? string.Empty;
                string type = worksheet.Cells[r, colType].Text?.Trim() ?? string.Empty;
                string hostName = worksheet.Cells[r, colHostName].Text?.Trim() ?? string.Empty;
                string sn = worksheet.Cells[r, colSN].Text?.Trim() ?? string.Empty;
                string assignedTo = worksheet.Cells[r, colAssignedTo].Text?.Trim() ?? string.Empty;
                string nrp = worksheet.Cells[r, colNRP].Text?.Trim() ?? string.Empty;
                string vendor = worksheet.Cells[r, colVendor].Text?.Trim() ?? string.Empty;
                string state = worksheet.Cells[r, colState].Text?.Trim() ?? string.Empty;
                string brand = worksheet.Cells[r, colBrand].Text?.Trim() ?? string.Empty;
                string model = worksheet.Cells[r, colModel].Text?.Trim() ?? string.Empty;
                string department = worksheet.Cells[r, colDepartment].Text?.Trim() ?? string.Empty;
                string division = worksheet.Cells[r, colDivision].Text?.Trim() ?? string.Empty;
                string os = worksheet.Cells[r, colOS].Text?.Trim() ?? string.Empty;
                string winVer = worksheet.Cells[r, colWinVer].Text?.Trim() ?? string.Empty;
                string category = worksheet.Cells[r, colCategory].Text?.Trim() ?? string.Empty;
                string ram = worksheet.Cells[r, colRam].Text?.Trim() ?? string.Empty;
                string storage = worksheet.Cells[r, colStorage].Text?.Trim() ?? string.Empty;
                string processor = worksheet.Cells[r, colProcessor].Text?.Trim() ?? string.Empty;
                string gpu = worksheet.Cells[r, colGpu].Text?.Trim() ?? string.Empty;
                string remarks = worksheet.Cells[r, colRemarks].Text?.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(sn) && string.IsNullOrEmpty(assetTag))
                    continue; // Skip empty rows

                // Clean hardware MAC/UUID format from Excel Column 5 (e.g. 00000000-0000-0000-0000-00D86110ACD1)
                if (sn.StartsWith("00000000-0000-0000-0000-", StringComparison.OrdinalIgnoreCase))
                {
                    sn = sn.Replace("00000000-0000-0000-0000-", "", StringComparison.OrdinalIgnoreCase).Trim();
                }

                if (string.IsNullOrEmpty(sn)) sn = assetTag;

                // 1. Auto-create Master Site
                Site? siteObj = null;
                if (!string.IsNullOrEmpty(district))
                {
                    string key = district.ToUpper();
                    if (!siteDict.TryGetValue(key, out siteObj))
                    {
                        siteObj = new Site { Id = Guid.NewGuid(), Code = district, Name = district };
                        _context.Sites.Add(siteObj);
                        await _context.SaveChangesAsync();
                        siteDict[key] = siteObj;
                    }
                }

                // 2. Auto-create Master Department
                Department? deptObj = null;
                if (!string.IsNullOrEmpty(department))
                {
                    string key = department.ToUpper();
                    if (!deptDict.TryGetValue(key, out deptObj))
                    {
                        deptObj = new Department { Id = Guid.NewGuid(), Name = department };
                        _context.Departments.Add(deptObj);
                        await _context.SaveChangesAsync();
                        deptDict[key] = deptObj;
                    }
                }

                // 3. Auto-create Master Division
                Division? divObj = null;
                if (!string.IsNullOrEmpty(division))
                {
                    string key = division.ToUpper();
                    if (!divDict.TryGetValue(key, out divObj))
                    {
                        divObj = new Division { Id = Guid.NewGuid(), Code = division, Name = division };
                        _context.Divisions.Add(divObj);
                        await _context.SaveChangesAsync();
                        divDict[key] = divObj;
                    }
                }

                // 4. Auto-create Master DeviceType
                DeviceType? typeObj = null;
                if (!string.IsNullOrEmpty(type))
                {
                    string key = type.ToUpper();
                    if (!typeDict.TryGetValue(key, out typeObj))
                    {
                        typeObj = new DeviceType { Id = Guid.NewGuid(), Name = type };
                        _context.DeviceTypes.Add(typeObj);
                        await _context.SaveChangesAsync();
                        typeDict[key] = typeObj;
                    }
                }

                // 5. Auto-create Master DeviceCategory
                DeviceCategory? catObj = null;
                if (!string.IsNullOrEmpty(category))
                {
                    string key = category.ToUpper();
                    if (!catDict.TryGetValue(key, out catObj))
                    {
                        catObj = new DeviceCategory { Id = Guid.NewGuid(), Name = category };
                        _context.DeviceCategories.Add(catObj);
                        await _context.SaveChangesAsync();
                        catDict[key] = catObj;
                    }
                }

                // 6. Auto-create Master Brand
                Brand? brandObj = null;
                if (!string.IsNullOrEmpty(brand))
                {
                    string key = brand.ToUpper();
                    if (!brandDict.TryGetValue(key, out brandObj))
                    {
                        brandObj = new Brand { Id = Guid.NewGuid(), Name = brand };
                        _context.Brands.Add(brandObj);
                        await _context.SaveChangesAsync();
                        brandDict[key] = brandObj;
                    }
                }

                // 7. Auto-create Master Vendor
                Vendor? vendorObj = null;
                if (!string.IsNullOrEmpty(vendor))
                {
                    string key = vendor.ToUpper();
                    if (!vendorDict.TryGetValue(key, out vendorObj))
                    {
                        vendorObj = new Vendor { Id = Guid.NewGuid(), Name = vendor };
                        _context.Vendors.Add(vendorObj);
                        await _context.SaveChangesAsync();
                        vendorDict[key] = vendorObj;
                    }
                }

                // 8. Auto-create Master Employee
                Employee? empObj = null;
                if (!string.IsNullOrEmpty(assignedTo))
                {
                    string key = assignedTo.ToUpper();
                    if (!empDict.TryGetValue(key, out empObj))
                    {
                        empObj = new Employee
                        {
                            Id = Guid.NewGuid(),
                            FullName = assignedTo,
                            NRP = nrp,
                            SiteId = siteObj?.Id,
                            DepartmentId = deptObj?.Id,
                            DivisionId = divObj?.Id
                        };
                        _context.Employees.Add(empObj);
                        await _context.SaveChangesAsync();
                        empDict[key] = empObj;
                        result.CreatedEmployees++;
                    }
                    else
                    {
                        if (empObj.NRP != nrp && !string.IsNullOrEmpty(nrp))
                        {
                            empObj.NRP = nrp;
                            _context.Employees.Update(empObj);
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                // 9. Upsert Asset
                string assetKey = sn.ToUpper();
                if (!assetDict.TryGetValue(assetKey, out var asset))
                {
                    asset = new Asset { Id = Guid.NewGuid() };
                    _context.Assets.Add(asset);
                }

                asset.NoSN = string.IsNullOrEmpty(assetTag) ? sn : assetTag;
                asset.SerialNumber = sn;
                asset.ComputerName = hostName;
                asset.Model = model;
                asset.OperatingSystem = os;
                asset.WindowsVersion = winVer;
                asset.Ram = ram;
                asset.Storage = storage;
                asset.Processor = processor;
                asset.Gpu = gpu;
                asset.Status = string.Equals(state, "In use", StringComparison.OrdinalIgnoreCase) ? "InUse" : (string.IsNullOrEmpty(state) ? "Available" : state);
                asset.Remarks = remarks;

                asset.DeviceTypeId = typeObj?.Id;
                asset.DeviceCategoryId = catObj?.Id;
                asset.BrandId = brandObj?.Id;
                asset.VendorId = vendorObj?.Id;

                // Fallbacks for string views
                asset.Site = district;
                asset.Department = department;
                asset.Division = division;
                asset.AssignedTo = assignedTo;
                asset.NRP = nrp;
                asset.DeviceType = type;
                asset.Brand = brand;

                await _context.SaveChangesAsync();
                assetDict[assetKey] = asset;
                result.ImportedAssets++;

                // 10. Upsert AssetAssignment (Data Pengguna Asset)
                if (empObj != null)
                {
                    var existingAssignment = await _context.AssetAssignments
                        .FirstOrDefaultAsync(a => a.AssetId == asset.Id && a.IsActive);

                    if (existingAssignment == null)
                    {
                        var newAssignment = new AssetAssignment
                        {
                            Id = Guid.NewGuid(),
                            AssetId = asset.Id,
                            EmployeeId = empObj.Id,
                            AssignDate = DateTime.Now,
                            IsActive = true,
                            Notes = "Imported from Excel"
                        };
                        _context.AssetAssignments.Add(newAssignment);
                        await _context.SaveChangesAsync();
                        result.ActiveAssignments++;
                    }
                    else if (existingAssignment.EmployeeId != empObj.Id)
                    {
                        existingAssignment.IsActive = false;
                        existingAssignment.ReturnDate = DateTime.Now;

                        var newAssignment = new AssetAssignment
                        {
                            Id = Guid.NewGuid(),
                            AssetId = asset.Id,
                            EmployeeId = empObj.Id,
                            AssignDate = DateTime.Now,
                            IsActive = true,
                            Notes = "Re-assigned via Excel Import"
                        };
                        _context.AssetAssignments.Add(newAssignment);
                        await _context.SaveChangesAsync();
                        result.ActiveAssignments++;
                    }
                }
            }

            result.Messages.Add($"Berhasil mengimpor {result.ImportedAssets} Aset, {result.CreatedEmployees} Employee Baru, dan {result.ActiveAssignments} Penugasan Pengguna Aset.");
            return result;
        }
    }
}
