# PANDUAN SETUP & ARSITEKTUR PROJEK (INFRAHUB & ITASSETTRACK)

Dokumen ini ditujukan sebagai panduan komprehensif bagi **Agent AI** maupun **Developer Selanjutnya** yang akan melanjutkan pengembangan, konfigurasi, deployment, dan pemeliharaan sistem ini.

---

## 1. Ringkasan Sistem & Arsitektur

Projek ini terdiri dari **2 aplikasi utama** berbasis **ASP.NET Core (.NET 8)** yang berjalan berdampingan:

| Komponen | Framework / Teknologi | Port Default | URL Lokal | Basis Data |
| :--- | :--- | :--- | :--- | :--- |
| **InfraHub.Web** | ASP.NET Core 8 MVC (Razor, SignalR, Dapper/EF) | `5000` | `http://localhost:5000` | `db_infrahub` |
| **ITAssetTrack.Web** | ASP.NET Core 8 MVC (Clean Architecture, EF Core) | `5001` | `http://localhost:5001` | `db_itassettrack` |

### Modul InfraHub (3 Sub-Sistem Terintegrasi)
InfraHub mengelola pemantauan infrastruktur dan armada secara terpadu melalui **Subsystem Switcher**:
1. **BTS Monitoring (`/Bts/*`)**: Pemantauan status menara BTS, rectifier, solar panel, sensor suhu shelter, generator, dan tower down anomalies.
2. **DC Infrastructure Monitoring (`/Ups/*`)**: Pemantauan Data Center, status kelistrikan UPS (3-phase input/output, battery backup), PAC (Precision Air Conditioning), sensor suhu/kelembaban, CCTV shelter, log anomali, dan verifikasi acknowledgement alarm.
3. **LTE Quality & Tracking (`/Tracking/*`)**: Pelacakan armada bergerak (Light Vehicles, Heavy Duty Trucks, Dump Trailers, Excavators) secara geospasial real-time, boundary polygon site, rute patroli, dan analitik kualitas sinyal LTE (RSRP/RSRQ/SINR).

### Modul ITAssetTrack (Asset & Maintenance Management)
1. **Asset Management (`/Asset/*`)**: Inventaris perangkat keras (Laptop, Desktop, Switch, Router, Monitor), tracking kepemilikan karyawan, serial number, status lifecycle.
2. **Preventive Maintenance (`/Maintenance/*`)**: Penjadwalan maintenance rutin, kalender checklist berkala, status penyelesaian tiket service.
3. **Employee Directory (`/Employee/*`)**: Data teknisi, staf, dan penugasan aset.

---

## 2. Prasyarat Lingkungan (Prerequisites)

- **.NET SDK 8.0** atau yang lebih baru (`dotnet --version`)
- **Microsoft SQL Server 2019 / 2022** (atau Express / Docker container) berjalan di port `1433`
- **SQL Server Command Line (`sqlcmd`)** atau SSMS / Azure Data Studio
- Peramban web modern (Google Chrome / Microsoft Edge / Firefox)

---

## 3. Konfigurasi Basis Data

Kedua aplikasi menggunakan SQL Server lokal dengan autentikasi `sa` atau Windows Authentication.

### A. Connection Strings
- **InfraHub (`InfraHub.Web/appsettings.json`)**:
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1,1433;Database=db_infrahub;User Id=sa;Password=[PASSWORD_ANDA];TrustServerCertificate=True;Encrypt=False;"
  }
  ```
- **ITAssetTrack (`ITAssetTrack/ITAssetTrack.Web/appsettings.json`)**:
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1,1433;Database=db_itassettrack;User Id=sa;Password=[PASSWORD_ANDA];TrustServerCertificate=True;Encrypt=False;"
  }
  ```

### B. Inisialisasi Database InfraHub (`db_infrahub`)
Jika setup dari awal:
1. Buat database:
   ```sql
   CREATE DATABASE db_infrahub;
   ```
2. Eksekusi script inisialisasi dari folder `database/init-scripts/`:
   - `01_restore_monitoring_schema.sql` (Tabel-tabel master `tbl_m_*`, roles, users)
   - `03_restore_monitoring_master.sql` (Data master sites, boundaries, devices - dengan kode generik `SITE_HQ`, `SITE_01` s/d `SITE_14`)
   - `04_create_procedures_monitoring.sql` (30 Stored Procedures untuk agregasi metrik dashboard, alert status, dan historical log)

### C. Inisialisasi Database ITAssetTrack (`db_itassettrack`)
1. Buat database:
   ```sql
   CREATE DATABASE db_itassettrack;
   ```
2. Jalankan migrasi EF Core:
   ```bash
   cd ITAssetTrack/ITAssetTrack.Web
   dotnet ef database update --project ../ITAssetTrack.Infrastructure
   ```

---

## 4. Akun Default & Kredensial Login

### A. InfraHub (`http://localhost:5000`)
| Username | Password | Role | Hak Akses Modul |
| :--- | :--- | :--- | :--- |
| `admin_infrahub` | `AdminPass123!` | `Administrator` | Akses penuh ke BTS, DC/UPS, LTE Tracking, dan Admin Manage |
| `noc_operator` | `AdminPass123!` | `NOC` | Akses ke BTS Monitoring & DC Infrastructure |
| `tracking_operator` | `AdminPass123!` | `Tracking` | Khusus akses modul LTE Quality & Fleet Tracking |

> **Catatan Teknis Hashing**: InfraHub menggunakan algoritma hashing **BCrypt** (`$2a$` format). Jika mengupdate password langsung via SQL, gunakan hash BCrypt ber-prefix `$2a$`, bukan hash legacy PHP `$2y$` agar dapat diverifikasi oleh library `BCrypt.Net-Next`.

### B. ITAssetTrack (`http://localhost:5001`)
| Username | Password | Role |
| :--- | :--- | :--- |
| `admin` | `AdminPass123!` | Administrator |
| `elyas` | `UserPass123!` | Technician / Staff |
| `franz` | `UserPass123!` | Technician / Staff |

> **Catatan Teknis Hashing**: ITAssetTrack menggunakan algoritma **PBKDF2-SHA256** melalui service `PasswordHasher.cs`.

---

## 5. Arsitektur Navbar & Subsystem Switcher (InfraHub)

Sebelumnya, seluruh tombol menu dari ketiga modul ditumpuk menjadi satu navbar panjang horizontal. Sekarang sistem menggunakan arsitektur **Contextual Subsystem Switcher**:

1. **Subsystem Switcher Dropdown (Kiri)**:
   - Terletak di sebelah kiri logo `InfraHub-BMS`.
   - Mengidentifikasi modul aktif secara otomatis dari routing URL (`/Bts/*`, `/Ups/*`, atau `/Tracking/*`).
   - Memungkinkan navigasi instan antar 3 domain:
     - 🗼 **BTS Monitoring** (`/Bts/Dashboard`)
     - ⚡ **DC Infrastructure** (`/Ups/Dashboard`)
     - 🛰️ **LTE Quality & Tracking** (`/Tracking/Live`)
2. **Contextual Menu Bar (Tengah)**:
   - Baris menu hanya menampilkan tautan halaman yang relevan dengan modul yang sedang dibuka.
   - Misalnya: saat di BTS Monitoring, hanya menu `Monitoring`, `Dashboard`, dan `Report & Analytics` yang terlihat.
3. **Role-Based Filtering (`RoleHelper.cs`)**:
   - Menu dan pilihan switcher difilter berdasarkan role pengguna saat ini.
   - Operator khusus Tracking hanya akan melihat opsi tracking; administrator melihat seluruh subsistem dan menu `Admin Manage` (`/Shared/ManageAreaSite`, `/Shared/ManageDevices`, `/Shared/ManageUserRole`).
4. **Ekstensibilitas Role**:
   - Jika di masa mendatang dibutuhkan pemisahan akun login terpisah per sub-sistem, konfigurasi izin di `InfraHub.Web/Core/Helpers/RoleHelper.cs` dapat disesuaikan tanpa merombak struktur UI.

---

## 6. Cara Menjalankan Aplikasi

### Menjalankan Secara Manual (Development CLI)

Buka dua jendela terminal terpisah:

**Terminal 1 (InfraHub)**:
```bash
cd "D:\Kuliah\Kelas 4 Semester 1\CleanWebProject\InfraHub.Web"
dotnet run --urls="http://localhost:5000"
```

**Terminal 2 (ITAssetTrack)**:
```bash
cd "D:\Kuliah\Kelas 4 Semester 1\CleanWebProject\ITAssetTrack\ITAssetTrack.Web"
dotnet run --urls="http://localhost:5001"
```

### Menjalankan via Docker Compose
Tersedia berkas `docker-compose.yml` di root direktori projek:
```bash
docker compose up -d --build
```

---

## 7. Simulator Telemetri & Pergerakan Unit (Python)

Tersedia simulator terpadu di `services/infrahub_simulator.py` yang menggantikan 14 script terpisah lama. Script ini mensimulasikan data telemetri real-time untuk 3 sub-sistem sekaligus:
1. **BTS Telemetry**: Metrik tower (RSSI, fuel level, mains status) & genset (3-phase voltage, current, power factor, engine hours)
2. **DC Infrastructure**: Metrik UPS (3-phase voltage, battery runtime, load percent), sensor suhu/kelembaban, PAC (setpoint, room temp, status code), dan CCTV online heartbeat
3. **LTE Quality & Fleet Tracking**: Pergerakan GPS dinamis (kecepatan, heading, status bergerak/idle), metrik LTE (RSRP, RSRQ, SINR, CQI, ping latency)

### Prasyarat Simulator:
```bash
uv pip install pyodbc
# atau: pip install pyodbc
```

### Menjalankan Simulator:
```bash
python services/infrahub_simulator.py
```
Simulator akan berjalan secara kontinyu dengan interval kirim 10 detik dan mencetak log batch per tick.

---

## 8. Struktur Direktori Projek

```text
CleanWebProject/
├── InfraHub.Web/                     # Aplikasi Web InfraHub (Port 5000)
│   ├── Areas/
│   │   ├── Bts/                      # Modul BTS Monitoring
│   │   ├── Ups/                      # Modul DC Infrastructure Monitoring
│   │   ├── Tracking/                 # Modul LTE Quality & Tracking
│   │   └── Shared/                   # Modul Auth, User Role & Area/Device Management
│   ├── Core/                         # DTO, Helpers, RoleHelper
│   ├── Infrastructure/               # Database Context, Repositories, Auth Services
│   ├── Views/Shared/_Layout.cshtml   # Master Layout + Subsystem Switcher
│   └── appsettings.json
│
├── ITAssetTrack/                     # Solusi ITAssetTrack (Port 5001)
│   ├── ITAssetTrack.Core/            # Entitas Domain & Interface
│   ├── ITAssetTrack.Infrastructure/  # EF Core DbContext, Migrasi & Repository
│   └── ITAssetTrack.Web/             # ASP.NET Core Web MVC & Controller
│       └── appsettings.json
│
├── database/                         # Script Inisialisasi SQL
│   └── init-scripts/
│       ├── 01_restore_monitoring_schema.sql
│       ├── 02_restore_asset_schema.sql
│       ├── 03_restore_monitoring_master.sql   # Master Sites (Generic)
│       └── 04_create_procedures_monitoring.sql # 30 Stored Procedures
│
├── services/                         # Layanan Latar Belakang & Simulator
│   └── infrahub_simulator.py         # Simulator Telemetri Terpadu (BTS, DC, Tracking)
│
├── SETUP_INSTRUCTION.md              # File panduan ini
└── docker-compose.yml                # Orkestrasi container
```

---
 
 ## 9. Panduan Troubleshooting

1. **Error `Login failed for user 'sa'`**:
   - Pastikan service SQL Server (`MSSQLSERVER` atau `SQLEXPRESS`) aktif.
   - Periksa apakah password `sa` pada `appsettings.json` sesuai dengan server lokal Anda.
2. **Error `Could not copy apphost.exe ... locked by process`**:
   - Terjadi saat menjalankan `dotnet build` saat aplikasi masih berjalan.
   - Hentikan proses yang berjalan (`Ctrl+C` pada terminal yang menjalankan `dotnet run`), lalu jalankan kembali `dotnet build`.
3. **Data Site Masih Muncul Nama Lama**:
   - Pastikan database `db_infrahub` telah menjalankan skrip pembersihan nama site generik dari `03_restore_monitoring_master.sql` atau lakukan eksekusi ulang update pada `tbl_m_sites`.
4. **Perubahan pada View `.cshtml` Tidak Kelihatan**:
   - Pada mode rilis atau build default .NET 8, Razor view dikompilasi ke assembly aplikasi. Lakukan restart aplikasi (`dotnet run`) setelah mengubah file `.cshtml` master layout.
