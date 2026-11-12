using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraHub.Web.Migrations
{
    /// <inheritdoc />
    public partial class CleanInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "personal_access_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tokenable_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    tokenable_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    abilities = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    last_used_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_access_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_sites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    site_code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    site_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    latitude = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: true),
                    longitude = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_sites", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_r_asset_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type_name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_r_asset_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_r_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_r_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_s_unregistered_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    serial_number = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    raw_payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    last_seen = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_s_unregistered_devices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_anomaly_acknowledges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    zone_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    panel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    anomaly_type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    is_acknowledged = table.Column<bool>(type: "bit", nullable: false),
                    acknowledged_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    acknowledged_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_anomaly_acknowledges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_fleet_daily_network_summary",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    device_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    site_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    total_points = table.Column<int>(type: "int", nullable: false),
                    points_inside_boundary = table.Column<int>(type: "int", nullable: false),
                    points_outside_boundary = table.Column<int>(type: "int", nullable: false),
                    total_excellent_rsrp = table.Column<int>(type: "int", nullable: false),
                    total_good_rsrp = table.Column<int>(type: "int", nullable: false),
                    total_fair_rsrp = table.Column<int>(type: "int", nullable: false),
                    total_poor_rsrp = table.Column<int>(type: "int", nullable: false),
                    total_very_poor_rsrp = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_fleet_daily_network_summary", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_site_basemaps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    site_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    provider_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    file_path = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_site_basemaps", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_m_site_basemaps_tbl_m_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "tbl_m_sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_site_boundaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    site_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    provider_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    file_path = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    polygon_data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_site_boundaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_m_site_boundaries_tbl_m_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "tbl_m_sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_site_coverages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    site_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    provider_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    file_path = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_site_coverages", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_m_site_coverages_tbl_m_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "tbl_m_sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_zones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    site_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    zone_code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    zone_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_zones", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_m_zones_tbl_m_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "tbl_m_sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    role_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_m_users_tbl_r_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "tbl_r_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    serial_number = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    imei = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    phone_number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    device_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    site_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    zone_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    asset_type_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    asset_height = table.Column<double>(type: "float", nullable: true),
                    fleet_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    network_area = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ups_phase_type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ups_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ups_category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    room_sensor_ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    ups_username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ups_password = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_devices", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_m_devices_tbl_m_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "tbl_m_sites",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_tbl_m_devices_tbl_m_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "tbl_m_zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_m_devices_tbl_r_asset_types_asset_type_id",
                        column: x => x.asset_type_id,
                        principalTable: "tbl_r_asset_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_r_user_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    site_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_r_user_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_r_user_assignments_tbl_m_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "tbl_m_sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_r_user_assignments_tbl_m_users_user_id",
                        column: x => x.user_id,
                        principalTable: "tbl_m_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_device_acknowledges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    acknowledged_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    start_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    evidence_path = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    verification_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    verified_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    verified_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    verification_notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_device_acknowledges", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_t_device_acknowledges_tbl_m_users_acknowledged_by",
                        column: x => x.acknowledged_by,
                        principalTable: "tbl_m_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_t_device_acknowledges_tbl_m_users_verified_by",
                        column: x => x.verified_by,
                        principalTable: "tbl_m_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_fleet_tracking_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "decimal(11,7)", precision: 11, scale: 7, nullable: true),
                    speed_kmh = table.Column<float>(type: "real", nullable: true),
                    heading_degree = table.Column<float>(type: "real", nullable: true),
                    altitude = table.Column<float>(type: "real", nullable: true),
                    gps_status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    satellite_in_use = table.Column<int>(type: "int", nullable: true),
                    network_rssi = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    network_rsrp = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    network_rsrq = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    network_sinr = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_fleet_tracking_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_t_fleet_tracking_logs_tbl_m_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "tbl_m_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_genset_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    voltage = table.Column<float>(type: "real", nullable: true),
                    current = table.Column<float>(type: "real", nullable: true),
                    power_kw = table.Column<float>(type: "real", nullable: true),
                    energy_kwh = table.Column<float>(type: "real", nullable: true),
                    frequency = table.Column<float>(type: "real", nullable: true),
                    power_factor = table.Column<float>(type: "real", nullable: true),
                    illuminance = table.Column<float>(type: "real", nullable: true),
                    fuel_level = table.Column<float>(type: "real", nullable: true),
                    latitude = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: true),
                    longitude = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true),
                    is_acknowledged = table.Column<bool>(type: "bit", nullable: false),
                    acknowledged_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    acknowledged_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_genset_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_t_genset_logs_tbl_m_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "tbl_m_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_tower_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    wind_speed = table.Column<float>(type: "real", nullable: true),
                    wind_direction = table.Column<float>(type: "real", nullable: true),
                    temperature = table.Column<float>(type: "real", nullable: true),
                    humidity = table.Column<float>(type: "real", nullable: true),
                    pressure = table.Column<float>(type: "real", nullable: true),
                    rainfall = table.Column<float>(type: "real", nullable: true),
                    illuminance = table.Column<float>(type: "real", nullable: true),
                    solar_radiation = table.Column<float>(type: "real", nullable: true),
                    pm2_5 = table.Column<float>(type: "real", nullable: true),
                    pm10 = table.Column<float>(type: "real", nullable: true),
                    tilt_x = table.Column<float>(type: "real", nullable: true),
                    tilt_y = table.Column<float>(type: "real", nullable: true),
                    ground_resistance = table.Column<float>(type: "real", nullable: true),
                    spd_count = table.Column<int>(type: "int", nullable: true),
                    lightning_current = table.Column<float>(type: "real", nullable: true),
                    deviation_m = table.Column<float>(type: "real", nullable: true),
                    weather_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    air_quality = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    wind_cardinal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    latitude = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: true),
                    longitude = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true),
                    is_acknowledged = table.Column<bool>(type: "bit", nullable: false),
                    acknowledged_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    acknowledged_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_tower_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_t_tower_logs_tbl_m_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "tbl_m_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_ups_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    voltage_in_a = table.Column<float>(type: "real", nullable: true),
                    voltage_in_b = table.Column<float>(type: "real", nullable: true),
                    voltage_in_c = table.Column<float>(type: "real", nullable: true),
                    current_in_a = table.Column<float>(type: "real", nullable: true),
                    current_in_b = table.Column<float>(type: "real", nullable: true),
                    current_in_c = table.Column<float>(type: "real", nullable: true),
                    input_frequency = table.Column<float>(type: "real", nullable: true),
                    voltage_out_a = table.Column<float>(type: "real", nullable: true),
                    voltage_out_b = table.Column<float>(type: "real", nullable: true),
                    voltage_out_c = table.Column<float>(type: "real", nullable: true),
                    current_out_a = table.Column<float>(type: "real", nullable: true),
                    current_out_b = table.Column<float>(type: "real", nullable: true),
                    current_out_c = table.Column<float>(type: "real", nullable: true),
                    output_frequency = table.Column<float>(type: "real", nullable: true),
                    battery_percent = table.Column<float>(type: "real", nullable: true),
                    battery_voltage = table.Column<float>(type: "real", nullable: true),
                    battery_runtime_min = table.Column<int>(type: "int", nullable: true),
                    load_percent = table.Column<float>(type: "real", nullable: true),
                    total_watts = table.Column<float>(type: "real", nullable: true),
                    ambient_temp = table.Column<float>(type: "real", nullable: true),
                    room_temp = table.Column<float>(type: "real", nullable: true),
                    ups_status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    grounding_resistance = table.Column<float>(type: "real", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_ups_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_t_ups_logs_tbl_m_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "tbl_m_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_m_devices_asset_type_id",
                table: "tbl_m_devices",
                column: "asset_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_m_devices_site_id",
                table: "tbl_m_devices",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_m_devices_zone_id",
                table: "tbl_m_devices",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_m_site_basemaps_site_id",
                table: "tbl_m_site_basemaps",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_m_site_boundaries_site_id",
                table: "tbl_m_site_boundaries",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_m_site_coverages_site_id",
                table: "tbl_m_site_coverages",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_m_users_role_id",
                table: "tbl_m_users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_m_zones_site_id",
                table: "tbl_m_zones",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_r_user_assignments_site_id",
                table: "tbl_r_user_assignments",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_r_user_assignments_user_id",
                table: "tbl_r_user_assignments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_anomaly_ack_scope",
                table: "tbl_t_anomaly_acknowledges",
                columns: new[] { "zone_id", "panel", "anomaly_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_device_acknowledges_acknowledged_by",
                table: "tbl_t_device_acknowledges",
                column: "acknowledged_by");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_device_acknowledges_verified_by",
                table: "tbl_t_device_acknowledges",
                column: "verified_by");

            migrationBuilder.CreateIndex(
                name: "idx_fleet_trace_optim",
                table: "tbl_t_fleet_tracking_logs",
                columns: new[] { "device_id", "device_time" });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_genset_logs_device_id",
                table: "tbl_t_genset_logs",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_tower_logs_device_id",
                table: "tbl_t_tower_logs",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "idx_ups_trace_optim",
                table: "tbl_t_ups_logs",
                columns: new[] { "device_id", "device_time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personal_access_tokens");

            migrationBuilder.DropTable(
                name: "tbl_m_site_basemaps");

            migrationBuilder.DropTable(
                name: "tbl_m_site_boundaries");

            migrationBuilder.DropTable(
                name: "tbl_m_site_coverages");

            migrationBuilder.DropTable(
                name: "tbl_r_user_assignments");

            migrationBuilder.DropTable(
                name: "tbl_s_unregistered_devices");

            migrationBuilder.DropTable(
                name: "tbl_t_anomaly_acknowledges");

            migrationBuilder.DropTable(
                name: "tbl_t_device_acknowledges");

            migrationBuilder.DropTable(
                name: "tbl_t_fleet_daily_network_summary");

            migrationBuilder.DropTable(
                name: "tbl_t_fleet_tracking_logs");

            migrationBuilder.DropTable(
                name: "tbl_t_genset_logs");

            migrationBuilder.DropTable(
                name: "tbl_t_tower_logs");

            migrationBuilder.DropTable(
                name: "tbl_t_ups_logs");

            migrationBuilder.DropTable(
                name: "tbl_m_users");

            migrationBuilder.DropTable(
                name: "tbl_m_devices");

            migrationBuilder.DropTable(
                name: "tbl_r_roles");

            migrationBuilder.DropTable(
                name: "tbl_m_zones");

            migrationBuilder.DropTable(
                name: "tbl_r_asset_types");

            migrationBuilder.DropTable(
                name: "tbl_m_sites");
        }
    }
}
