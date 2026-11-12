# Implementation Plan: Preparation Staging (SQL Server Cloud)

Dokumen ini berisi panduan dan rencana persiapan deployment **Fase Staging** untuk menguji penyimpanan data langsung ke **Database SQL Server Cloud** dari PC Workstation.

---

## Status Framework & Environment Saat Ini

- **Target Framework:** `.NET 8.0` (`net8.0`) — *(Sudah direvert kembali dari .NET 9.0)*
- **ORM / Database Provider:** `Microsoft.EntityFrameworkCore.SqlServer` (`8.0.10`)
- **Status Build:** **Succeeded** (0 Error) — Siap untuk di-push ke Git repository.

---

## Analisis EF Core & SeedData untuk Staging

### 1. EF Core Auto-Migration & Resiliency
- **Automatic Migration:** Di dalam `DbInitializer.cs`, perintah `context.Database.Migrate()` telah dikonfigurasi. Saat aplikasi pertama kali dijalankan di PC Workstation dengan koneksi SQL Server Cloud, EF Core akan **otomatis membuat tabel & skema database terbaru** tanpa perlu jalankan skrip SQL manual.
- **Connection Resiliency (Retry Logic):** Di `Program.cs`, sudah terpasang `.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5))`. Ini sangat berguna untuk koneksi **SQL Server Cloud** untuk mengantisipasi kestabilan jaringan/latensi.

### 2. Initial Data Seeding (`DbInitializer` & `SeedData`)
- **Akun Default (`AppUsers`):** Otomatis di-seed jika tabel user di Cloud DB masih kosong:
  - `admin` / `Password123!` (Role: Admin)
  - `coordinator` / `Password123!` (Role: Coordinator)
  - `technician` / `Password123!` (Role: Technician)
  - `viewer` / `Password123!` (Role: Viewer)
- **Master Excel Seeding:** `SeedData.cs` akan otomatis mendeteksi file Excel master `Asset PC & NB_HQ_DEPT_NRP.xlsx` di folder project dan memasukkan data aset awal jika database cloud belum memiliki data aset.

---

## Checklist Persiapan Staging di PC Workstation

### Langkah 1: Push dari PC Local & Pull di PC Workstation
1. Di PC Local (saat ini), commit dan push seluruh perubahan ke Git:
   ```bash
   git add .
   git commit -m "Revert to .NET 8.0 & Prep Staging for SQL Server Cloud"
   git push origin main
   ```
2. Di PC Workstation (Staging), pull perubahan terbaru:
   ```bash
   git pull origin main
   ```

### Langkah 2: Konfigurasi Connection String SQL Server Cloud
Ubah `ConnectionStrings:DefaultConnection` pada [appsettings.json](file:///c:/webserver/www/ITAssetTrack/ITAssetTrack.Web/appsettings.json) atau buat file `appsettings.Staging.json` di PC Workstation:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=asset_sysdv401.infrahub.net;Database=db_itassettrack;User Id=user_infra_dept_app;Password=infrahub.123;TrustServerCertificate=true;"
  }
}
```
> [!IMPORTANT]
> Pastikan Port **1433** / Server `asset_sysdv401.infrahub.net` dan database `db_itassettrack` (skema `itassettrack`) dapat diakses dengan user `user_infra_dept_app`.

### Langkah 3: Eksekusi Python Telemetry Hub Server (`server_hub_with_insert.py`)
1. Script `Services/server_hub_with_insert.py` dan `Services/server_hub.py` telah dikonfigurasi menggunakan target database `db_itassettrack`.
2. Telemetry insert menyasar tabel berikut pada skema `itassettrack` yang digenerate oleh EF Core Migration .NET 8:
   - `itassettrack.tbl_t_asset`
   - `itassettrack.tbl_t_agent_reports`
   - `itassettrack.tbl_t_agent_top_apps`
   - `itassettrack.tbl_t_agent_battery_timelines`
   - `itassettrack.tbl_s_unregistered_assets`
   - `itassettrack.tbl_r_asset_assignment` & `itassettrack.tbl_m_employee` (untuk verifikasi Mismatch User)

### Langkah 3: Eksekusi & Test Save Data di Staging
1. Jalankan aplikasi di PC Workstation:
   ```bash
   dotnet run --project ITAssetTrack.Web/ITAssetTrack.Web.csproj
   ```
2. Aplikasi akan otomatis:
   - Terhubung ke SQL Server Cloud.
   - Menjalankan EF Core Migration (`Database.Migrate()`).
   - Melakukan seeding akun default & master data jika database masih baru.
3. Lakukan pengujian **Trial Save Data**:
   - Login sebagai `admin` atau `technician`.
   - Coba tambah/edit Aset, input Preventative Maintenance, atau buat Berita Acara Serah Terima (BAST).
   - Pastikan data tersimpan dengan benar di SQL Server Cloud.

---

## Proposed Changes (Dokumentasi File Kunci)

#### [ITAssetTrack.Core.csproj](file:///c:/webserver/www/ITAssetTrack/ITAssetTrack.Core/ITAssetTrack.Core.csproj)
- TargetFramework: `net8.0`

#### [ITAssetTrack.Infrastructure.csproj](file:///c:/webserver/www/ITAssetTrack/ITAssetTrack.Infrastructure/ITAssetTrack.Infrastructure.csproj)
- TargetFramework: `net8.0`, Packages EF Core: `8.0.10`

#### [ITAssetTrack.Web.csproj](file:///c:/webserver/www/ITAssetTrack/ITAssetTrack.Web/ITAssetTrack.Web.csproj)
- TargetFramework: `net8.0`, Packages EF Core: `8.0.10`

#### [appsettings.json](file:///c:/webserver/www/ITAssetTrack/ITAssetTrack.Web/appsettings.json)
- Siap disesuaikan `ConnectionStrings` ke Cloud Database saat staging.

---

## Verification Plan

### Manual Verification on Workstation PC
1. Test koneksi SQL Cloud menggunakan perintah:
   ```bash
   dotnet ef database update --project ITAssetTrack.Infrastructure --startup-project ITAssetTrack.Web
   ```
   *(Atau langsung run aplikasi karena `context.Database.Migrate()` sudah terpasang di startup).*
2. Verifikasi login user `admin` (`Password123!`).
3. Verifikasi simpan data baru di Cloud DB.
