using InfraHub.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfraHub.Infrastructure.Data;

/// <summary>
/// AppDbContext — maps to existing SQL Server database (MonitoringDB).
/// Designed for Database-First approach: no migrations will be generated from here,
/// since tables already exist from Laravel migrations.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ──── Master Tables ────
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<AssetType> AssetTypes => Set<AssetType>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<UserAssignment> UserAssignments => Set<UserAssignment>();
    public DbSet<UnregisteredDevice> UnregisteredDevices => Set<UnregisteredDevice>();

    // ──── Transaction / Log Tables ────
    public DbSet<GensetLog> GensetLogs => Set<GensetLog>();
    public DbSet<TowerLog> TowerLogs => Set<TowerLog>();
    public DbSet<UpsLog> UpsLogs => Set<UpsLog>();
    public DbSet<FleetTrackingLog> FleetTrackingLogs => Set<FleetTrackingLog>();
    public DbSet<AnomalyAcknowledge> AnomalyAcknowledges => Set<AnomalyAcknowledge>();
    public DbSet<DeviceAcknowledge> DeviceAcknowledges => Set<DeviceAcknowledge>();
    public DbSet<FleetDailyNetworkSummary> FleetDailyNetworkSummaries => Set<FleetDailyNetworkSummary>();

    // ──── Site Geo Tables ────
    public DbSet<SiteBasemap> SiteBasemaps => Set<SiteBasemap>();
    public DbSet<SiteBoundary> SiteBoundaries => Set<SiteBoundary>();
    public DbSet<SiteCoverage> SiteCoverages => Set<SiteCoverage>();

    // ──── Auth Tables ────
    public DbSet<PersonalAccessToken> PersonalAccessTokens => Set<PersonalAccessToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ══════════════════════════════════════════════
        // Role
        // ══════════════════════════════════════════════
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("tbl_r_roles");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.RoleName).HasColumnName("role_name").IsRequired();
            e.Property(r => r.Description).HasColumnName("description");
        });

        // ══════════════════════════════════════════════
        // User
        // ══════════════════════════════════════════════
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("tbl_m_users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.Username).HasColumnName("username").IsRequired();
            e.Property(u => u.Name).HasColumnName("name").IsRequired();
            e.Property(u => u.Email).HasColumnName("email");
            e.Property(u => u.Password).HasColumnName("password").IsRequired();
            e.Property(u => u.RoleId).HasColumnName("role_id");
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
            e.Property(u => u.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(u => u.Role)
             .WithMany(r => r.Users)
             .HasForeignKey(u => u.RoleId);
        });

        // ══════════════════════════════════════════════
        // Site
        // ══════════════════════════════════════════════
        modelBuilder.Entity<Site>(e =>
        {
            e.ToTable("tbl_m_sites");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id");
            e.Property(s => s.SiteCode).HasColumnName("site_code").IsRequired();
            e.Property(s => s.SiteName).HasColumnName("site_name").IsRequired();
            e.Property(s => s.Latitude).HasColumnName("latitude").HasPrecision(10, 8);
            e.Property(s => s.Longitude).HasColumnName("longitude").HasPrecision(11, 8);
        });

        // ══════════════════════════════════════════════
        // Zone
        // ══════════════════════════════════════════════
        modelBuilder.Entity<Zone>(e =>
        {
            e.ToTable("tbl_m_zones");
            e.HasKey(z => z.Id);
            e.Property(z => z.Id).HasColumnName("id");
            e.Property(z => z.SiteId).HasColumnName("site_id");
            e.Property(z => z.ZoneCode).HasColumnName("zone_code").IsRequired();
            e.Property(z => z.ZoneName).HasColumnName("zone_name").IsRequired();
            e.Property(z => z.Description).HasColumnName("description");
            e.Property(z => z.CreatedBy).HasColumnName("created_by");
            e.Property(z => z.UpdatedBy).HasColumnName("updated_by");
            e.Property(z => z.CreatedAt).HasColumnName("created_at");
            e.Property(z => z.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(z => z.Site)
             .WithMany(s => s.Zones)
             .HasForeignKey(z => z.SiteId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════
        // AssetType
        // ══════════════════════════════════════════════
        modelBuilder.Entity<AssetType>(e =>
        {
            e.ToTable("tbl_r_asset_types");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.TypeName).HasColumnName("type_name").IsRequired();
        });

        // ══════════════════════════════════════════════
        // Device
        // ══════════════════════════════════════════════
        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("tbl_m_devices");
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasColumnName("id");
            e.Property(d => d.SerialNumber).HasColumnName("serial_number").IsRequired();
            e.Property(d => d.Imei).HasColumnName("imei").HasMaxLength(255);
            e.Property(d => d.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
            e.Property(d => d.DeviceName).HasColumnName("device_name").IsRequired();
            e.Property(d => d.SiteId).HasColumnName("site_id");
            e.Property(d => d.ZoneId).HasColumnName("zone_id");
            e.Property(d => d.AssetTypeId).HasColumnName("asset_type_id");
            e.Property(d => d.AssetHeight).HasColumnName("asset_height");
            e.Property(d => d.FleetType).HasColumnName("fleet_type").HasMaxLength(50);
            e.Property(d => d.NetworkArea).HasColumnName("network_area");
            e.Property(d => d.UpsPhaseType).HasColumnName("ups_phase_type").HasMaxLength(10);
            e.Property(d => d.UpsType).HasColumnName("ups_type").HasMaxLength(50);
            e.Property(d => d.UpsCategory).HasColumnName("ups_category");
            e.Property(d => d.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            e.Property(d => d.RoomSensorIp).HasColumnName("room_sensor_ip").HasMaxLength(45);
            e.Property(d => d.UpsUsername).HasColumnName("ups_username").HasMaxLength(100);
            e.Property(d => d.UpsPassword).HasColumnName("ups_password").HasMaxLength(100);
            e.Property(d => d.IsActive).HasColumnName("is_active");
            e.Property(d => d.CreatedAt).HasColumnName("created_at");
            e.Property(d => d.CreatedBy).HasColumnName("created_by");
            e.Property(d => d.UpdatedAt).HasColumnName("updated_at");
            e.Property(d => d.UpdatedBy).HasColumnName("updated_by");

            e.HasOne(d => d.Site)
             .WithMany(s => s.Devices)
             .HasForeignKey(d => d.SiteId);

            e.HasOne(d => d.Zone)
             .WithMany(z => z.Devices)
             .HasForeignKey(d => d.ZoneId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(d => d.AssetType)
             .WithMany(a => a.Devices)
             .HasForeignKey(d => d.AssetTypeId);
        });

        // ══════════════════════════════════════════════
        // UserAssignment
        // ══════════════════════════════════════════════
        modelBuilder.Entity<UserAssignment>(e =>
        {
            e.ToTable("tbl_r_user_assignments");
            e.HasKey(ua => ua.Id);
            e.Property(ua => ua.Id).HasColumnName("id");
            e.Property(ua => ua.UserId).HasColumnName("user_id");
            e.Property(ua => ua.SiteId).HasColumnName("site_id");
            e.Property(ua => ua.CreatedBy).HasColumnName("created_by");
            e.Property(ua => ua.CreatedAt).HasColumnName("created_at");
            e.Property(ua => ua.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(ua => ua.User)
             .WithMany(u => u.UserAssignments)
             .HasForeignKey(ua => ua.UserId);

            e.HasOne(ua => ua.Site)
             .WithMany(s => s.UserAssignments)
             .HasForeignKey(ua => ua.SiteId);
        });

        // ══════════════════════════════════════════════
        // UnregisteredDevice
        // ══════════════════════════════════════════════
        modelBuilder.Entity<UnregisteredDevice>(e =>
        {
            e.ToTable("tbl_s_unregistered_devices");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.SerialNumber).HasColumnName("serial_number").IsRequired();
            e.Property(u => u.RawPayload).HasColumnName("raw_payload");
            e.Property(u => u.LastSeen).HasColumnName("last_seen");
            e.Property(u => u.CreatedBy).HasColumnName("created_by");
            e.Property(u => u.UpdatedBy).HasColumnName("updated_by");
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
            e.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        });

        // ══════════════════════════════════════════════
        // GensetLog
        // ══════════════════════════════════════════════
        modelBuilder.Entity<GensetLog>(e =>
        {
            e.ToTable("tbl_t_genset_logs");
            e.HasKey(g => g.Id);
            e.Property(g => g.Id).HasColumnName("id");
            e.Property(g => g.DeviceId).HasColumnName("device_id");
            e.Property(g => g.DeviceTime).HasColumnName("device_time");
            e.Property(g => g.Voltage).HasColumnName("voltage");
            e.Property(g => g.Current).HasColumnName("current");
            e.Property(g => g.PowerKw).HasColumnName("power_kw");
            e.Property(g => g.EnergyKwh).HasColumnName("energy_kwh");
            e.Property(g => g.Frequency).HasColumnName("frequency");
            e.Property(g => g.PowerFactor).HasColumnName("power_factor");
            e.Property(g => g.Illuminance).HasColumnName("illuminance");
            e.Property(g => g.FuelLevel).HasColumnName("fuel_level");
            e.Property(g => g.Latitude).HasColumnName("latitude").HasPrecision(10, 8);
            e.Property(g => g.Longitude).HasColumnName("longitude").HasPrecision(11, 8);
            e.Property(g => g.IsAcknowledged).HasColumnName("is_acknowledged");
            e.Property(g => g.AcknowledgedAt).HasColumnName("acknowledged_at");
            e.Property(g => g.AcknowledgedBy).HasColumnName("acknowledged_by");
            e.Property(g => g.CreatedAt).HasColumnName("created_at");
            e.Property(g => g.CreatedBy).HasColumnName("created_by");
            e.Property(g => g.UpdatedAt).HasColumnName("updated_at");
            e.Property(g => g.UpdatedBy).HasColumnName("updated_by");

            e.HasOne(g => g.Device)
             .WithMany(d => d.GensetLogs)
             .HasForeignKey(g => g.DeviceId);
        });

        // ══════════════════════════════════════════════
        // TowerLog
        // ══════════════════════════════════════════════
        modelBuilder.Entity<TowerLog>(e =>
        {
            e.ToTable("tbl_t_tower_logs");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasColumnName("id");
            e.Property(t => t.DeviceId).HasColumnName("device_id");
            e.Property(t => t.DeviceTime).HasColumnName("device_time");
            e.Property(t => t.WindSpeed).HasColumnName("wind_speed");
            e.Property(t => t.WindDirection).HasColumnName("wind_direction");
            e.Property(t => t.Temperature).HasColumnName("temperature");
            e.Property(t => t.Humidity).HasColumnName("humidity");
            e.Property(t => t.Pressure).HasColumnName("pressure");
            e.Property(t => t.Rainfall).HasColumnName("rainfall");
            e.Property(t => t.Illuminance).HasColumnName("illuminance");
            e.Property(t => t.SolarRadiation).HasColumnName("solar_radiation");
            e.Property(t => t.Pm2_5).HasColumnName("pm2_5");
            e.Property(t => t.Pm10).HasColumnName("pm10");
            e.Property(t => t.TiltX).HasColumnName("tilt_x");
            e.Property(t => t.TiltY).HasColumnName("tilt_y");
            e.Property(t => t.GroundResistance).HasColumnName("ground_resistance");
            e.Property(t => t.SpdCount).HasColumnName("spd_count");
            e.Property(t => t.LightningCurrent).HasColumnName("lightning_current");
            e.Property(t => t.DeviationM).HasColumnName("deviation_m");
            e.Property(t => t.WeatherStatus).HasColumnName("weather_status").HasMaxLength(50);
            e.Property(t => t.AirQuality).HasColumnName("air_quality").HasMaxLength(50);
            e.Property(t => t.WindCardinal).HasColumnName("wind_cardinal").HasMaxLength(20);
            e.Property(t => t.Latitude).HasColumnName("latitude").HasPrecision(10, 8);
            e.Property(t => t.Longitude).HasColumnName("longitude").HasPrecision(11, 8);
            e.Property(t => t.IsAcknowledged).HasColumnName("is_acknowledged");
            e.Property(t => t.AcknowledgedAt).HasColumnName("acknowledged_at");
            e.Property(t => t.AcknowledgedBy).HasColumnName("acknowledged_by");
            e.Property(t => t.CreatedAt).HasColumnName("created_at");
            e.Property(t => t.CreatedBy).HasColumnName("created_by");
            e.Property(t => t.UpdatedAt).HasColumnName("updated_at");
            e.Property(t => t.UpdatedBy).HasColumnName("updated_by");

            e.HasOne(t => t.Device)
             .WithMany(d => d.TowerLogs)
             .HasForeignKey(t => t.DeviceId);
        });

        // ══════════════════════════════════════════════
        // UpsLog
        // ══════════════════════════════════════════════
        modelBuilder.Entity<UpsLog>(e =>
        {
            e.ToTable("tbl_t_ups_logs");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.DeviceId).HasColumnName("device_id");
            e.Property(u => u.DeviceTime).HasColumnName("device_time");
            e.Property(u => u.VoltageInA).HasColumnName("voltage_in_a");
            e.Property(u => u.VoltageInB).HasColumnName("voltage_in_b");
            e.Property(u => u.VoltageInC).HasColumnName("voltage_in_c");
            e.Property(u => u.CurrentInA).HasColumnName("current_in_a");
            e.Property(u => u.CurrentInB).HasColumnName("current_in_b");
            e.Property(u => u.CurrentInC).HasColumnName("current_in_c");
            e.Property(u => u.InputFrequency).HasColumnName("input_frequency");
            e.Property(u => u.VoltageOutA).HasColumnName("voltage_out_a");
            e.Property(u => u.VoltageOutB).HasColumnName("voltage_out_b");
            e.Property(u => u.VoltageOutC).HasColumnName("voltage_out_c");
            e.Property(u => u.CurrentOutA).HasColumnName("current_out_a");
            e.Property(u => u.CurrentOutB).HasColumnName("current_out_b");
            e.Property(u => u.CurrentOutC).HasColumnName("current_out_c");
            e.Property(u => u.OutputFrequency).HasColumnName("output_frequency");
            e.Property(u => u.BatteryPercent).HasColumnName("battery_percent");
            e.Property(u => u.BatteryVoltage).HasColumnName("battery_voltage");
            e.Property(u => u.BatteryRuntimeMin).HasColumnName("battery_runtime_min");
            e.Property(u => u.LoadPercent).HasColumnName("load_percent");
            e.Property(u => u.TotalWatts).HasColumnName("total_watts");
            e.Property(u => u.AmbientTemp).HasColumnName("ambient_temp");
            e.Property(u => u.RoomTemp).HasColumnName("room_temp");
            e.Property(u => u.UpsStatus).HasColumnName("ups_status");
            e.Property(u => u.GroundingResistance).HasColumnName("grounding_resistance");
            e.Property(u => u.CreatedAt).HasColumnName("created_at");

            e.HasOne(u => u.Device)
             .WithMany(d => d.UpsLogs)
             .HasForeignKey(u => u.DeviceId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(new[] { "DeviceId", "DeviceTime" }).HasDatabaseName("idx_ups_trace_optim");
        });

        // ══════════════════════════════════════════════
        // FleetTrackingLog
        // ══════════════════════════════════════════════
        modelBuilder.Entity<FleetTrackingLog>(e =>
        {
            e.ToTable("tbl_t_fleet_tracking_logs");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).HasColumnName("id");
            e.Property(f => f.DeviceId).HasColumnName("device_id");
            e.Property(f => f.DeviceTime).HasColumnName("device_time");
            e.Property(f => f.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
            e.Property(f => f.Longitude).HasColumnName("longitude").HasPrecision(11, 7);
            e.Property(f => f.SpeedKmh).HasColumnName("speed_kmh");
            e.Property(f => f.HeadingDegree).HasColumnName("heading_degree");
            e.Property(f => f.Altitude).HasColumnName("altitude");
            e.Property(f => f.GpsStatus).HasColumnName("gps_status");
            e.Property(f => f.SatelliteInUse).HasColumnName("satellite_in_use");
            e.Property(f => f.NetworkRssi).HasColumnName("network_rssi").HasPrecision(10, 2);
            e.Property(f => f.NetworkRsrp).HasColumnName("network_rsrp").HasPrecision(10, 2);
            e.Property(f => f.NetworkRsrq).HasColumnName("network_rsrq").HasPrecision(10, 2);
            e.Property(f => f.NetworkSinr).HasColumnName("network_sinr").HasPrecision(10, 2);
            e.Property(f => f.CreatedAt).HasColumnName("created_at");

            e.HasOne(f => f.Device)
             .WithMany(d => d.FleetTrackingLogs)
             .HasForeignKey(f => f.DeviceId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(new[] { "DeviceId", "DeviceTime" }).HasDatabaseName("idx_fleet_trace_optim");
        });

        // ══════════════════════════════════════════════
        // AnomalyAcknowledge
        // ══════════════════════════════════════════════
        modelBuilder.Entity<AnomalyAcknowledge>(e =>
        {
            e.ToTable("tbl_t_anomaly_acknowledges");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.ZoneId).HasColumnName("zone_id");
            e.Property(a => a.Panel).HasColumnName("panel").HasMaxLength(30);
            e.Property(a => a.AnomalyType).HasColumnName("anomaly_type").HasMaxLength(60);
            e.Property(a => a.IsAcknowledged).HasColumnName("is_acknowledged");
            e.Property(a => a.AcknowledgedAt).HasColumnName("acknowledged_at");
            e.Property(a => a.AcknowledgedBy).HasColumnName("acknowledged_by");
            e.Property(a => a.CreatedAt).HasColumnName("created_at");
            e.Property(a => a.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(new[] { "ZoneId", "Panel", "AnomalyType" })
             .IsUnique()
             .HasDatabaseName("uq_anomaly_ack_scope");
        });

        // ══════════════════════════════════════════════
        // DeviceAcknowledge
        // ══════════════════════════════════════════════
        modelBuilder.Entity<DeviceAcknowledge>(e =>
        {
            e.ToTable("tbl_t_device_acknowledges");
            e.HasKey(da => da.Id);
            e.Property(da => da.Id).HasColumnName("id");
            e.Property(da => da.DeviceId).HasColumnName("device_id").IsRequired();
            e.Property(da => da.AcknowledgedBy).HasColumnName("acknowledged_by").IsRequired();
            e.Property(da => da.StartTime).HasColumnName("start_time").IsRequired();
            e.Property(da => da.EndTime).HasColumnName("end_time").IsRequired();
            e.Property(da => da.Reason).HasColumnName("reason").HasMaxLength(100).IsRequired();
            e.Property(da => da.Notes).HasColumnName("notes");
            e.Property(da => da.EvidencePath).HasColumnName("evidence_path");
            e.Property(da => da.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(da => da.VerificationStatus).HasColumnName("verification_status").HasMaxLength(20).IsRequired();
            e.Property(da => da.VerifiedBy).HasColumnName("verified_by");
            e.Property(da => da.VerifiedAt).HasColumnName("verified_at");
            e.Property(da => da.VerificationNotes).HasColumnName("verification_notes");

            e.HasOne(da => da.User)
             .WithMany()
             .HasForeignKey(da => da.AcknowledgedBy)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(da => da.Verifier)
             .WithMany()
             .HasForeignKey(da => da.VerifiedBy)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ══════════════════════════════════════════════
        // FleetDailyNetworkSummary
        // ══════════════════════════════════════════════
        modelBuilder.Entity<FleetDailyNetworkSummary>(e =>
        {
            e.ToTable("tbl_t_fleet_daily_network_summary");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).HasColumnName("id");
            e.Property(f => f.Date).HasColumnName("date");
            e.Property(f => f.DeviceId).HasColumnName("device_id");
            e.Property(f => f.SiteCode).HasColumnName("site_code");
            e.Property(f => f.TotalPoints).HasColumnName("total_points");
            e.Property(f => f.PointsInsideBoundary).HasColumnName("points_inside_boundary");
            e.Property(f => f.PointsOutsideBoundary).HasColumnName("points_outside_boundary");
            e.Property(f => f.TotalExcellentRsrp).HasColumnName("total_excellent_rsrp");
            e.Property(f => f.TotalGoodRsrp).HasColumnName("total_good_rsrp");
            e.Property(f => f.TotalFairRsrp).HasColumnName("total_fair_rsrp");
            e.Property(f => f.TotalPoorRsrp).HasColumnName("total_poor_rsrp");
            e.Property(f => f.TotalVeryPoorRsrp).HasColumnName("total_very_poor_rsrp");
            e.Property(f => f.CreatedAt).HasColumnName("created_at");
            e.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        });

        // ══════════════════════════════════════════════
        // Site Geo Entities
        // ══════════════════════════════════════════════
        modelBuilder.Entity<SiteBasemap>(e =>
        {
            e.ToTable("tbl_m_site_basemaps");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id");
            e.Property(s => s.SiteId).HasColumnName("site_id");
            e.Property(s => s.ProviderName).HasColumnName("provider_name");
            e.Property(s => s.FilePath).HasColumnName("file_path");
            e.Property(s => s.IsActive).HasColumnName("is_active");
            e.Property(s => s.CreatedAt).HasColumnName("created_at");
            e.Property(s => s.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(s => s.Site).WithMany(s => s.SiteBasemaps).HasForeignKey(s => s.SiteId);
        });

        modelBuilder.Entity<SiteBoundary>(e =>
        {
            e.ToTable("tbl_m_site_boundaries");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id");
            e.Property(s => s.SiteId).HasColumnName("site_id");
            e.Property(s => s.ProviderName).HasColumnName("provider_name");
            e.Property(s => s.FilePath).HasColumnName("file_path");
            e.Property(s => s.PolygonData).HasColumnName("polygon_data");
            e.Property(s => s.IsActive).HasColumnName("is_active");
            e.Property(s => s.CreatedAt).HasColumnName("created_at");
            e.Property(s => s.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(s => s.Site).WithMany(s => s.SiteBoundaries).HasForeignKey(s => s.SiteId);
        });

        modelBuilder.Entity<SiteCoverage>(e =>
        {
            e.ToTable("tbl_m_site_coverages");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id");
            e.Property(s => s.SiteId).HasColumnName("site_id");
            e.Property(s => s.ProviderName).HasColumnName("provider_name");
            e.Property(s => s.FilePath).HasColumnName("file_path");
            e.Property(s => s.IsActive).HasColumnName("is_active");
            e.Property(s => s.CreatedAt).HasColumnName("created_at");
            e.Property(s => s.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(s => s.Site).WithMany(s => s.SiteCoverages).HasForeignKey(s => s.SiteId);
        });

        // ══════════════════════════════════════════════
        // PersonalAccessToken
        // ══════════════════════════════════════════════
        modelBuilder.Entity<PersonalAccessToken>(e =>
        {
            e.ToTable("personal_access_tokens");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.TokenableType).HasColumnName("tokenable_type");
            e.Property(p => p.TokenableId).HasColumnName("tokenable_id");
            e.Property(p => p.Name).HasColumnName("name");
            e.Property(p => p.Token).HasColumnName("token").HasMaxLength(64);
            e.Property(p => p.Abilities).HasColumnName("abilities");
            e.Property(p => p.LastUsedAt).HasColumnName("last_used_at");
            e.Property(p => p.ExpiresAt).HasColumnName("expires_at");
            e.Property(p => p.CreatedAt).HasColumnName("created_at");
            e.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
