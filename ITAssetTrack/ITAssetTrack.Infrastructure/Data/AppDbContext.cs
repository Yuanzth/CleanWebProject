using Microsoft.EntityFrameworkCore;
using ITAssetTrack.Models;

namespace ITAssetTrack.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Master Tables (tbl_m_...)
        public DbSet<Site> Sites { get; set; } = null!;
        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<Division> Divisions { get; set; } = null!;
        public DbSet<DeviceType> DeviceTypes { get; set; } = null!;
        public DbSet<DeviceCategory> DeviceCategories { get; set; } = null!;
        public DbSet<Brand> Brands { get; set; } = null!;
        public DbSet<Vendor> Vendors { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<TroubleCategory> TroubleCategories { get; set; } = null!;
        public DbSet<AppUser> AppUsers { get; set; } = null!;

        // Transaction Tables (tbl_t_...)
        public DbSet<Asset> Assets { get; set; } = null!;
        public DbSet<PmPlan> PmPlans { get; set; } = null!;
        public DbSet<ScheduledMaint> ScheduledMaints { get; set; } = null!;
        public DbSet<UnscheduledMaint> UnscheduledMaints { get; set; } = null!;
        public DbSet<SerahTerima> SerahTerimas { get; set; } = null!;

        // Relational / Detail Tables (tbl_r_...)
        public DbSet<AssetAssignment> AssetAssignments { get; set; } = null!;
        public DbSet<MaintenancePhoto> MaintenancePhotos { get; set; } = null!;
        public DbSet<SerahTerimaPhoto> SerahTerimaPhotos { get; set; } = null!;

        // Agent Telemetry & Staging Tables
        public DbSet<AgentReport> AgentReports { get; set; } = null!;
        public DbSet<AgentTopApp> AgentTopApps { get; set; } = null!;
        public DbSet<AgentAppTimeline> AgentAppTimelines { get; set; } = null!;
        public DbSet<AgentInstalledSoftware> AgentInstalledSoftware { get; set; } = null!;
        public DbSet<AgentBatteryTimeline> AgentBatteryTimelines { get; set; } = null!;
        public DbSet<AgentLoginSession> AgentLoginSessions { get; set; } = null!;
        public DbSet<UnregisteredAsset> UnregisteredAssets { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Default Schema for ITAssetTrack
            modelBuilder.HasDefaultSchema("itassettrack");

            // Table Name Prefix Mapping
            modelBuilder.Entity<Site>().ToTable("tbl_m_site");
            modelBuilder.Entity<Department>().ToTable("tbl_m_department");
            modelBuilder.Entity<Division>().ToTable("tbl_m_division");
            modelBuilder.Entity<DeviceType>().ToTable("tbl_m_device_type");
            modelBuilder.Entity<DeviceCategory>().ToTable("tbl_m_device_category");
            modelBuilder.Entity<Brand>().ToTable("tbl_m_brand");
            modelBuilder.Entity<Vendor>().ToTable("tbl_m_vendor");
            modelBuilder.Entity<Employee>().ToTable("tbl_m_employee");
            modelBuilder.Entity<TroubleCategory>().ToTable("tbl_m_trouble_category");
            modelBuilder.Entity<AppUser>().ToTable("tbl_m_user");

            modelBuilder.Entity<Asset>().ToTable("tbl_t_asset");
            modelBuilder.Entity<PmPlan>().ToTable("tbl_t_pm_plan");
            modelBuilder.Entity<ScheduledMaint>().ToTable("tbl_t_scheduled_maint");
            modelBuilder.Entity<UnscheduledMaint>().ToTable("tbl_t_unscheduled_maint");
            modelBuilder.Entity<SerahTerima>().ToTable("tbl_t_serah_terima");

            modelBuilder.Entity<AssetAssignment>().ToTable("tbl_r_asset_assignment");
            modelBuilder.Entity<MaintenancePhoto>().ToTable("tbl_r_maintenance_photo");
            modelBuilder.Entity<SerahTerimaPhoto>().ToTable("tbl_r_serah_terima_photo");

            modelBuilder.Entity<AgentReport>().ToTable("tbl_t_agent_reports");
            modelBuilder.Entity<AgentTopApp>().ToTable("tbl_t_agent_top_apps");
            modelBuilder.Entity<AgentAppTimeline>().ToTable("tbl_t_agent_app_timelines");
            modelBuilder.Entity<AgentInstalledSoftware>().ToTable("tbl_t_agent_installed_software");
            modelBuilder.Entity<AgentBatteryTimeline>().ToTable("tbl_t_agent_battery_timelines");
            modelBuilder.Entity<AgentLoginSession>().ToTable("tbl_t_agent_login_sessions");
            modelBuilder.Entity<UnregisteredAsset>().ToTable("tbl_s_unregistered_assets");

            // Unique Indexes
            modelBuilder.Entity<Asset>()
                .HasIndex(a => a.SerialNumber)
                .IsUnique();

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Employee FKs (Restrict delete to prevent accidental cascade)
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Site)
                .WithMany(s => s.Employees)
                .HasForeignKey(e => e.SiteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Department)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Division)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DivisionId)
                .OnDelete(DeleteBehavior.Restrict);

            // AssetAssignment FKs
            modelBuilder.Entity<AssetAssignment>()
                .HasOne(a => a.Asset)
                .WithMany(a => a.AssetAssignments)
                .HasForeignKey(a => a.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AssetAssignment>()
                .HasOne(a => a.Employee)
                .WithMany(e => e.AssetAssignments)
                .HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
