using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAssetTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "itassettrack");

            migrationBuilder.CreateTable(
                name: "tbl_m_brand",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_brand", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_department",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_department", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_device_category",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_device_category", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_device_type",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_device_type", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_division",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_division", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_site",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_site", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_trouble_category",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_trouble_category", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_user",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NRP = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Site = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_user", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_vendor",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_vendor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_r_maintenance_photo",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaintenanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhotoType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_r_maintenance_photo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_r_serah_terima_photo",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerahTerimaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_r_serah_terima_photo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_s_unregistered_assets",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Hostname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LoggedInUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OsVersion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LocalIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    Ssid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(10,8)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_s_unregistered_assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_pm_plan",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MonthYear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetQuantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_pm_plan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_scheduled_maint",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanMonth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Tanggal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    StartTimeRecorded = table.Column<bool>(type: "bit", nullable: false),
                    NoDocument = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NamaUser = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NRP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactInfo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Site = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Division = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComputerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Processor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessorKondisi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ram = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RamKondisi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Storage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StorageKondisi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WindowsVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceKondisi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyboardAda = table.Column<bool>(type: "bit", nullable: false),
                    MouseAda = table.Column<bool>(type: "bit", nullable: false),
                    MonitorAda = table.Column<bool>(type: "bit", nullable: false),
                    ChargerAda = table.Column<bool>(type: "bit", nullable: false),
                    TasLaptopAda = table.Column<bool>(type: "bit", nullable: false),
                    MultiportAda = table.Column<bool>(type: "bit", nullable: false),
                    KeyboardBrand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyboardKondisi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MouseBrand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MouseKondisi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MonitorBrand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MonitorModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MonitorKondisi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyboardSN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MouseSN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MonitorSN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FisikKondisiBaik = table.Column<bool>(type: "bit", nullable: false),
                    UEFIPassword = table.Column<bool>(type: "bit", nullable: false),
                    BootOrderStorage = table.Column<bool>(type: "bit", nullable: false),
                    BootOrderDisable = table.Column<bool>(type: "bit", nullable: false),
                    DisableAdmin = table.Column<bool>(type: "bit", nullable: false),
                    DiskCleanUp = table.Column<bool>(type: "bit", nullable: false),
                    SpaceSSD = table.Column<bool>(type: "bit", nullable: false),
                    Keluhan = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContactedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactMethod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OriginalPlanDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reschedule1Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reschedule1Sig = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reschedule1Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reschedule2Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reschedule2Sig = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reschedule2Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reschedule3Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reschedule3Sig = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reschedule3Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PmStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PmCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserHandoverSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserNotPresent = table.Column<bool>(type: "bit", nullable: false),
                    ReturnNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecBySignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeaderSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItosSectionHeadSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeaderApproved = table.Column<bool>(type: "bit", nullable: false),
                    LeaderApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaderApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SectionHeadApproved = table.Column<bool>(type: "bit", nullable: false),
                    SectionHeadApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SectionHeadApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_scheduled_maint", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_serah_terima",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoDocument = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tipe = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NamaUser = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NRP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Site = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Division = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Series = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComputerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperatingSystem = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdditionalHardware = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ram = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Storage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyboardSN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MouseSN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MonitorSN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Alasan = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdminSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NamaPenyerah = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NamaPenerima = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NamaDiketahui = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignaturePenyerah = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignaturePenerima = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignatureDiketahui = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_serah_terima", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_unscheduled_maint",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TroubleCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionTaken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NamaUser = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Site = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComputerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Processor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ram = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Storage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WindowsVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyboardSN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MouseSN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MonitorSN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeaderSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SectionHeadSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeaderApproved = table.Column<bool>(type: "bit", nullable: false),
                    SectionHeadApproved = table.Column<bool>(type: "bit", nullable: false),
                    LeaderApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SectionHeadApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeaderApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SectionHeadApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_unscheduled_maint", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_m_employee",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NRP = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_m_employee", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_m_employee_tbl_m_department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_m_department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_m_employee_tbl_m_division_DivisionId",
                        column: x => x.DivisionId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_m_division",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_m_employee_tbl_m_site_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_m_site",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_asset",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoSN = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ComputerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OperatingSystem = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WindowsVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ram = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Storage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Processor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Gpu = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeviceCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeviceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Series = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Site = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Division = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NRP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyboardSN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MouseSN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MonitorSN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdditionalHardware = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastOnlineTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsAFK = table.Column<bool>(type: "bit", nullable: false),
                    Uptime = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActiveApps = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    LastLoggedInUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastBatteryHealthPct = table.Column<int>(type: "int", nullable: true),
                    IsOnline = table.Column<bool>(type: "bit", nullable: false),
                    IsUserMismatch = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_asset", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_t_asset_tbl_m_brand_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_m_brand",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tbl_t_asset_tbl_m_device_category_DeviceCategoryId",
                        column: x => x.DeviceCategoryId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_m_device_category",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tbl_t_asset_tbl_m_device_type_DeviceTypeId",
                        column: x => x.DeviceTypeId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_m_device_type",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tbl_t_asset_tbl_m_vendor_VendorId",
                        column: x => x.VendorId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_m_vendor",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_r_asset_assignment",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_r_asset_assignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_r_asset_assignment_tbl_m_employee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_m_employee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_r_asset_assignment_tbl_t_asset_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_t_asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_agent_reports",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Hostname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LoggedInUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AssignedNrp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsUserMismatch = table.Column<bool>(type: "bit", nullable: false),
                    OsVersion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReportType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ScheduleTriggered = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RecordIntervalSeconds = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CpuUptimeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    BatteryHealthPct = table.Column<int>(type: "int", nullable: true),
                    LocalIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Ssid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,8)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                    CpuMinPct = table.Column<int>(type: "int", nullable: false),
                    CpuAvgPct = table.Column<int>(type: "int", nullable: false),
                    CpuMaxPct = table.Column<int>(type: "int", nullable: false),
                    RamMinPct = table.Column<int>(type: "int", nullable: false),
                    RamAvgPct = table.Column<int>(type: "int", nullable: false),
                    RamMaxPct = table.Column<int>(type: "int", nullable: false),
                    GpuMinPct = table.Column<int>(type: "int", nullable: false),
                    GpuAvgPct = table.Column<int>(type: "int", nullable: false),
                    GpuMaxPct = table.Column<int>(type: "int", nullable: false),
                    DailyNetworkDownloadMb = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    DailyNetworkUploadMb = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    DailyNetworkTotalMb = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    DailyNetworkUsageText = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_agent_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_t_agent_reports_tbl_t_asset_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_t_asset",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_agent_app_timelines",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AppName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StartTime = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EndTime = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DurationMinutes = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsAFK = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_agent_app_timelines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_t_agent_app_timelines_tbl_t_agent_reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_t_agent_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_agent_battery_timelines",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartTime = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EndTime = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StartPct = table.Column<int>(type: "int", nullable: false),
                    EndPct = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_agent_battery_timelines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_t_agent_battery_timelines_tbl_t_agent_reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_t_agent_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_agent_installed_software",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SoftwareName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SoftwareVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    InstallDate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_agent_installed_software", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_t_agent_installed_software_tbl_t_agent_reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_t_agent_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_agent_login_sessions",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Hostname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LoggedInUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SessionStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SessionEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_agent_login_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_t_agent_login_sessions_tbl_t_agent_reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_t_agent_reports",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_t_agent_top_apps",
                schema: "itassettrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AppName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RecordCount = table.Column<int>(type: "int", nullable: false),
                    EstimatedDurationMinutes = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsAFK = table.Column<bool>(type: "bit", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_t_agent_top_apps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_t_agent_top_apps_tbl_t_agent_reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "itassettrack",
                        principalTable: "tbl_t_agent_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_m_employee_DepartmentId",
                schema: "itassettrack",
                table: "tbl_m_employee",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_m_employee_DivisionId",
                schema: "itassettrack",
                table: "tbl_m_employee",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_m_employee_SiteId",
                schema: "itassettrack",
                table: "tbl_m_employee",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_m_user_Username",
                schema: "itassettrack",
                table: "tbl_m_user",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_r_asset_assignment_AssetId",
                schema: "itassettrack",
                table: "tbl_r_asset_assignment",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_r_asset_assignment_EmployeeId",
                schema: "itassettrack",
                table: "tbl_r_asset_assignment",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_agent_app_timelines_ReportId",
                schema: "itassettrack",
                table: "tbl_t_agent_app_timelines",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_agent_battery_timelines_ReportId",
                schema: "itassettrack",
                table: "tbl_t_agent_battery_timelines",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_agent_installed_software_ReportId",
                schema: "itassettrack",
                table: "tbl_t_agent_installed_software",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_agent_login_sessions_ReportId",
                schema: "itassettrack",
                table: "tbl_t_agent_login_sessions",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_agent_reports_AssetId",
                schema: "itassettrack",
                table: "tbl_t_agent_reports",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_agent_top_apps_ReportId",
                schema: "itassettrack",
                table: "tbl_t_agent_top_apps",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_asset_BrandId",
                schema: "itassettrack",
                table: "tbl_t_asset",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_asset_DeviceCategoryId",
                schema: "itassettrack",
                table: "tbl_t_asset",
                column: "DeviceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_asset_DeviceTypeId",
                schema: "itassettrack",
                table: "tbl_t_asset",
                column: "DeviceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_asset_SerialNumber",
                schema: "itassettrack",
                table: "tbl_t_asset",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_t_asset_VendorId",
                schema: "itassettrack",
                table: "tbl_t_asset",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_m_trouble_category",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_m_user",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_r_asset_assignment",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_r_maintenance_photo",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_r_serah_terima_photo",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_s_unregistered_assets",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_t_agent_app_timelines",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_t_agent_battery_timelines",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_t_agent_installed_software",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_t_agent_login_sessions",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_t_agent_top_apps",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_t_pm_plan",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_t_scheduled_maint",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_t_serah_terima",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_t_unscheduled_maint",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_m_employee",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_t_agent_reports",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_m_department",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_m_division",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_m_site",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_t_asset",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_m_brand",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_m_device_category",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_m_device_type",
                schema: "itassettrack");

            migrationBuilder.DropTable(
                name: "tbl_m_vendor",
                schema: "itassettrack");
        }
    }
}
