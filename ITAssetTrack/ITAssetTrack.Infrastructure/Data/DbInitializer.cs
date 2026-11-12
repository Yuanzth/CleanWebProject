using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ITAssetTrack.Infrastructure.Services;
using ITAssetTrack.Models;

namespace ITAssetTrack.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            // EnsureCreated diganti CanConnect() agar tidak menjalankan query berat saat startup.
            // Database sudah ada — cukup verifikasi koneksi berhasil.
            if (!context.Database.CanConnect())
            {
                throw new Exception("Tidak dapat terhubung ke database. Pastikan koneksi SQL Server db_itassettrack aktif.");
            }

            try
            {
                // Ensure database is migrated cleanly via EF Core Migrations if pending
                context.Database.Migrate();
            }
            catch
            {
                // Ignore if tables/objects already exist in target schema
            }

            // Fix missing telemetry columns in tbl_t_asset if they are absent
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_asset') AND name = 'IsOnline')
                    ALTER TABLE itassettrack.tbl_t_asset ADD IsOnline bit NOT NULL DEFAULT 0;
                
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_asset') AND name = 'IsUserMismatch')
                    ALTER TABLE itassettrack.tbl_t_asset ADD IsUserMismatch bit NOT NULL DEFAULT 0;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_asset') AND name = 'LastBatteryHealthPct')
                    ALTER TABLE itassettrack.tbl_t_asset ADD LastBatteryHealthPct int NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_asset') AND name = 'LastIp')
                    ALTER TABLE itassettrack.tbl_t_asset ADD LastIp nvarchar(45) NOT NULL DEFAULT '';

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_asset') AND name = 'LastLoggedInUser')
                    ALTER TABLE itassettrack.tbl_t_asset ADD LastLoggedInUser nvarchar(100) NOT NULL DEFAULT '';

                -- Columns for Unscheduled PM Approvals
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_unscheduled_maint') AND name = 'LeaderSignature')
                    ALTER TABLE itassettrack.tbl_t_unscheduled_maint ADD LeaderSignature nvarchar(max) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_unscheduled_maint') AND name = 'SectionHeadSignature')
                    ALTER TABLE itassettrack.tbl_t_unscheduled_maint ADD SectionHeadSignature nvarchar(max) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_unscheduled_maint') AND name = 'LeaderApproved')
                    ALTER TABLE itassettrack.tbl_t_unscheduled_maint ADD LeaderApproved bit NOT NULL DEFAULT 0;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_unscheduled_maint') AND name = 'SectionHeadApproved')
                    ALTER TABLE itassettrack.tbl_t_unscheduled_maint ADD SectionHeadApproved bit NOT NULL DEFAULT 0;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_unscheduled_maint') AND name = 'LeaderApprovedBy')
                    ALTER TABLE itassettrack.tbl_t_unscheduled_maint ADD LeaderApprovedBy nvarchar(100) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_unscheduled_maint') AND name = 'SectionHeadApprovedBy')
                    ALTER TABLE itassettrack.tbl_t_unscheduled_maint ADD SectionHeadApprovedBy nvarchar(100) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_unscheduled_maint') AND name = 'LeaderApprovedAt')
                    ALTER TABLE itassettrack.tbl_t_unscheduled_maint ADD LeaderApprovedAt datetime2 NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('itassettrack.tbl_t_unscheduled_maint') AND name = 'SectionHeadApprovedAt')
                    ALTER TABLE itassettrack.tbl_t_unscheduled_maint ADD SectionHeadApprovedAt datetime2 NULL;
            ");


            // Target user accounts matching team structure
            var targetUsers = new[]
            {
                new { Username = "admin", FullName = "Admin", Role = "Admin", NRP = "P000001" },
                new { Username = "elyas", FullName = "Elyas", Role = "Technician", NRP = "P000003" },
                new { Username = "putra", FullName = "Putra", Role = "Technician", NRP = "P000005" },
                new { Username = "franz", FullName = "Franz", Role = "Approver1", NRP = "P000006" },
                new { Username = "eddi", FullName = "Eddi", Role = "Approver2", NRP = "P000004" },
                // Aliases for compatibility
                new { Username = "teknisi1", FullName = "Elyas", Role = "Technician", NRP = "P000003" },
                new { Username = "teknisi2", FullName = "Putra", Role = "Technician", NRP = "P000005" },
                new { Username = "sechead1", FullName = "Franz", Role = "Approver1", NRP = "P000006" },
                new { Username = "sechead2", FullName = "Franz", Role = "Approver1", NRP = "P000007" },
                new { Username = "depthead", FullName = "Eddi", Role = "Approver2", NRP = "P000004" }
            };

            foreach (var u in targetUsers)
            {
                var existingUser = context.AppUsers.FirstOrDefault(x => x.Username.ToLower() == u.Username.ToLower());
                if (existingUser == null)
                {
                    context.AppUsers.Add(new AppUser
                    {
                        Id = Guid.NewGuid(),
                        Username = u.Username,
                        FullName = u.FullName,
                        NRP = u.NRP,
                        PasswordHash = PasswordHasher.HashPassword("Password123!"),
                        Role = u.Role,
                        Site = "HQ_DEPT",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    });
                }
                else
                {
                    existingUser.FullName = u.FullName;
                    existingUser.Role = u.Role;
                    existingUser.NRP = u.NRP;
                    existingUser.PasswordHash = PasswordHasher.HashPassword("Password123!");
                    context.AppUsers.Update(existingUser);
                }
            }
            context.SaveChanges();

            // Update existing approval history names to reflect clean Franz & Eddi names
            context.Database.ExecuteSqlRaw(@"
                UPDATE itassettrack.tbl_m_user SET FullName = 'Franz' WHERE Username IN ('franz', 'sechead1', 'sechead2');
                UPDATE itassettrack.tbl_m_user SET FullName = 'Eddi' WHERE Username IN ('eddi', 'depthead');
                UPDATE itassettrack.tbl_t_unscheduled_maint SET LeaderApprovedBy = 'Franz' WHERE LeaderApprovedBy IN ('sechead1', 'sechead2', 'Approver 1', 'PIC TEAM', 'Pak Franz');
                UPDATE itassettrack.tbl_t_unscheduled_maint SET SectionHeadApprovedBy = 'Eddi' WHERE SectionHeadApprovedBy IN ('depthead', 'Approver 2', 'Section Head', 'Pak Eddi');
                UPDATE itassettrack.tbl_t_scheduled_maint SET LeaderApprovedBy = 'Franz' WHERE LeaderApprovedBy IN ('sechead1', 'sechead2', 'Approver 1', 'PIC TEAM', 'Pak Franz');
                UPDATE itassettrack.tbl_t_scheduled_maint SET SectionHeadApprovedBy = 'Eddi' WHERE SectionHeadApprovedBy IN ('depthead', 'Approver 2', 'Section Head', 'Pak Eddi');
                UPDATE itassettrack.tbl_t_scheduled_maint SET PerformedBy = '' WHERE Status IN ('Planned', 'Belum PM') OR PerformedBy = 'Elyas';
            ");

            // Seed a demo employee for Viewer role (P000004) if not exists
            var viewerEmp = context.Employees.FirstOrDefault(e => e.NRP == "P000004");
            if (viewerEmp == null)
            {
                var site = context.Sites.FirstOrDefault();
                if (site == null)
                {
                    site = new Site { Id = Guid.NewGuid(), Code = "HQ_DEPT", Name = "Jakarta Industrial Estate Pulogadung" };
                    context.Sites.Add(site);
                }

                var dept = context.Departments.FirstOrDefault();
                if (dept == null)
                {
                    dept = new Department { Id = Guid.NewGuid(), Name = "Information Technology" };
                    context.Departments.Add(dept);
                }

                var div = context.Divisions.FirstOrDefault();
                if (div == null)
                {
                    div = new Division { Id = Guid.NewGuid(), Code = "ITOS", Name = "IT Operation & Support" };
                    context.Divisions.Add(div);
                }

                context.SaveChanges();

                viewerEmp = new Employee
                {
                    Id = Guid.NewGuid(),
                    NRP = "P000004",
                    FullName = "Auditor / Guest Viewer",
                    Email = "auditor.viewer@infrahub.com",
                    Phone = "+6281234567890",
                    SiteId = site.Id,
                    DepartmentId = dept.Id,
                    DivisionId = div.Id,
                    CreatedAt = DateTime.Now
                };
                context.Employees.Add(viewerEmp);
                context.SaveChanges();

                // Seed a mock asset for this employee
                var asset = new Asset
                {
                    Id = Guid.NewGuid(),
                    SerialNumber = "SN-GUEST-LAPTOP-12345",
                    NoSN = "NO-SN-998877",
                    ComputerName = "HQ_DEPTLAPGUEST01",
                    DeviceType = "Notebook",
                    Brand = "Dell",
                    Model = "Latitude 5430",
                    Processor = "Intel Core i5-1245U",
                    Ram = "16 GB",
                    Storage = "512 GB SSD",
                    OperatingSystem = "Windows 11 Pro",
                    WindowsVersion = "23H2",
                    Status = "InUse",
                    AssignedTo = "Auditor / Guest Viewer",
                    NRP = "P000004",
                    Site = "HQ_DEPT",
                    Division = "ITOS",
                    CreatedAt = DateTime.Now
                };
                context.Assets.Add(asset);
                context.SaveChanges();

                // Create the assignment
                var assignment = new AssetAssignment
                {
                    Id = Guid.NewGuid(),
                    AssetId = asset.Id,
                    EmployeeId = viewerEmp.Id,
                    AssignDate = DateTime.Now.AddMonths(-3),
                    IsActive = true,
                    Notes = "Laptop dinas Auditor Utama"
                };
                context.AssetAssignments.Add(assignment);

                // Add a mock Scheduled PM item in the past and one in the future
                var pm1 = new ScheduledMaint
                {
                    Id = Guid.NewGuid(),
                    PlanMonth = DateTime.Today.ToString("yyyy-MM"),
                    PlanDate = DateTime.Today.AddDays(-2),
                    ActualDate = DateTime.Today.AddDays(-2),
                    Status = "Completed",
                    SerialNumber = asset.SerialNumber,
                    NamaUser = viewerEmp.FullName,
                    NRP = viewerEmp.NRP,
                    Site = "HQ_DEPT",
                    Division = "ITOS",
                    ComputerName = asset.ComputerName,
                    DeviceType = asset.DeviceType,
                    Brand = asset.Brand,
                    Model = asset.Model,
                    Processor = asset.Processor,
                    Ram = asset.Ram,
                    Storage = asset.Storage,
                    WindowsVersion = asset.WindowsVersion,
                    PerformedBy = "ITAssetTrack Technician A",
                    UserHandoverSignature = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='100' height='50'><path d='M10,25 Q30,5 50,25 T90,25' fill='none' stroke='black' stroke-width='2'/></svg>",
                    ExecBySignature = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='100' height='50'><path d='M10,25 L90,25' fill='none' stroke='black' stroke-width='2'/></svg>",
                    Remark = "Pembersihan debu fisik, cek kestabilan OS. OK."
                };
                var pm2 = new ScheduledMaint
                {
                    Id = Guid.NewGuid(),
                    PlanMonth = DateTime.Today.AddMonths(1).ToString("yyyy-MM"),
                    PlanDate = DateTime.Today.AddMonths(1).AddDays(5),
                    Status = "Planned",
                    SerialNumber = asset.SerialNumber,
                    NamaUser = viewerEmp.FullName,
                    NRP = viewerEmp.NRP,
                    Site = "HQ_DEPT",
                    Division = "ITOS",
                    ComputerName = asset.ComputerName,
                    DeviceType = asset.DeviceType,
                    Brand = asset.Brand,
                    Model = asset.Model,
                    Processor = asset.Processor,
                    Ram = asset.Ram,
                    Storage = asset.Storage,
                    WindowsVersion = asset.WindowsVersion
                };
                context.ScheduledMaints.AddRange(pm1, pm2);
                context.SaveChanges();
            }
        }
    }
}
