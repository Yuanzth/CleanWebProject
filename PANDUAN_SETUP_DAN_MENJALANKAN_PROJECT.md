# 📘 Panduan Setup & Menjalankan Project InfraHub Internship di Laptop Baru

Dokumen ini adalah panduan lengkap dan terverifikasi untuk menjalankan 2 aplikasi web magang InfraHub beserta database dan generator data dummy di laptop masing-masing anggota tim.

---

## 🏗️ Gambaran Proyek

| Project | Modul & Fitur | Tech Stack | Port Lokal | Database |
| :--- | :--- | :--- | :--- | :--- |
| **InfraHub** (`InfraHub.Web`) | BTS Monitoring, UPS Telemetry, PAC, Genset, Tower Stability, Armada Tracking | ASP.NET Core 8 MVC, EF Core, ADO.NET SP | `http://localhost:5000` | `db_infrahub` (23 Tabel, 30 Stored Procedures) |
| **ITAssetTrack** (`ITAssetTrack`) | Inventaris Hardware (Laptop/PC), Scheduled PM Checklist, Unscheduled Maint, Serah Terima | ASP.NET Core 8 MVC, EF Core | `http://localhost:5001` | `db_itassettrack` (28 Tabel) |
| **Data Telemetry Generator** (`data-generator`) | Injeksi otomatis data telemetri UPS & heartbeat laptop setiap 30 detik | Python 3, pymssql, Faker | Service Latar Belakang | Terhubung ke kedua database |

---

## 📋 Prasyarat di Laptop Masing-Masing

Sebelum memulai, pastikan laptop sudah memiliki:
1. **.NET 8.0 SDK** ([Unduh dari Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0))
   - Verifikasi di terminal:
     ```bash
     dotnet --version
     # Output harus 8.0.xxx
     ```
2. **Database Engine (Pilih salah satu):**
   - **Pilihan 1 (Direkomendasikan jika sudah ada SQL Server):** Microsoft SQL Server 2019/2022 (Developer / Express Edition) + `sqlcmd`.
   - **Pilihan 2 (Jika tidak mau install SQL Server lokal):** Docker Desktop (Docker Engine aktif).
3. **Python 3.9+** (Jika ingin menjalankan generator data dummy secara native tanpa Docker).

---

## 🚀 Langkah 1: Kloning Repositori

```bash
git clone <URL_REPO_GITHUB_BARU>
cd BackupWebProject
```

---

## 🗄️ Langkah 2: Menyiapkan Database

Pilih **Metode A** (Native SQL Server) atau **Metode B** (Docker Container).

### 🔹 Metode A: Menggunakan SQL Server Lokal Laptop (Native)

1. Pastikan Service SQL Server Anda berjalan:
   - Buka PowerShell as Administrator:
     ```powershell
     net start MSSQLSERVER
     # atau jika pakai SQLEXPRESS: net start MSSQL$SQLEXPRESS
     ```
2. Pastikan akun `sa` aktif dengan password `Admin123#`:
   - Buka SQL Server Management Studio (SSMS) atau PowerShell, pastikan mixed-mode authentication (SQL Server and Windows Authentication) aktif, dan login `sa` diaktifkan dengan password: `Admin123#`.
3. Jalankan script restore otomatis:
   - **Klik ganda file `restore_local_sqlserver.bat`**, ATAU jalankan di terminal:
     ```cmd
     restore_local_sqlserver.bat
     ```
   - Script akan otomatis mengeksekusi urutan:
     1. `01_init_databases.sql`: Membuat database `db_infrahub` dan `db_itassettrack`.
     2. `02_restore_itassettrack.sql`: Restore seluruh 28 tabel & data master Asset Track.
     3. `03_restore_monitoring_master.sql`: Restore seluruh 23 tabel master data Monitoring Hub.
     4. `04_create_procedures_monitoring.sql`: Mengompilasi seluruh 30 Stored Procedures lengkap.

---

### 🔹 Metode B: Menggunakan Docker Desktop

Jika tidak ingin menginstall SQL Server di laptop:
1. Pastikan Docker Desktop sudah aktif.
2. **Klik ganda file `start_docker_infra.bat`**, ATAU jalankan perintah:
   ```bash
   docker compose up -d --build
   ```
3. Docker akan otomatis:
   - Menjalankan container SQL Server 2022 di `localhost:1433`.
   - Me-restore kedua database dan 30 stored procedures secara otomatis.
   - Menjalankan container `infrahub-data-generator` yang langsung menyuplai data live.

---

## ⚡ Langkah 3: Menjalankan Generator Data Dummy (Opsional tapi Direkomendasikan)

Agar grafik telemetri UPS, status sensor baterai laptop, dan tracking armada terisi data hidup:

- **Jika pakai Docker (Metode B):** Generator sudah otomatis berjalan di dalam container!
- **Jika pakai SQL Server Lokal (Metode A):**
  1. Install dependensi python (cukup 1 kali):
     ```bash
     pip install -r data-generator/requirements.txt
     ```
  2. **Klik ganda `start_generator_local.bat`**
  3. Generator akan aktif di terminal terpisah dan mengirimkan telemetri baru setiap 30 detik.

---

## 💻 Langkah 4: Menjalankan Kedua Aplikasi Web (.NET)

Cukup **klik ganda file `start_local_apps.bat`**!

File batch ini akan otomatis membuka 2 jendela konsol:
1. **Jendela 1: InfraHub** -> `http://localhost:5000`
2. **Jendela 2: ITAssetTrack** -> `http://localhost:5001`

*(Atau jika ingin menjalankan manual via terminal):*
```bash
# Terminal 1 (Monitoring Hub)
cd InfraHub.Web
dotnet run --urls="http://localhost:5000"

# Terminal 2 (Asset Track)
cd ITAssetTrack/ITAssetTrack.Web
dotnet run --urls="http://localhost:5001"
```

---

## 🔑 Akun & Kredensial Login

### 1. InfraHub (`http://localhost:5000`)

| Role | Username | Password | Deskripsi Akses |
| :--- | :--- | :--- | :--- |
| **Administrator** | `admin_infrahub` | `AdminPass123!` | Akses penuh dashboard BTS, Master Data, Site Management |
| **NOC Operator** | `noc_operator` | `AdminPass123!` | Akses dashboard UPS, Event Log, Alarm Ack |
| **Tracking Operator** | `tracking_operator` | `AdminPass123!` | Akses Fleet Tracking Live, Route History, Armada Units |
| **Viewer Site** | `viewer_indo` / `viewer_brcb` | `AdminPass123!` | Akses terbatas hanya ke site bersangkutan |

*Catatan: Password `AdminPass123!` adalah developer master bypass yang aktif di lingkungan lokal.*

### 2. ITAssetTrack (`http://localhost:5001`)

| Role | Username | Password | Deskripsi Akses |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin` | `Password123!` | Manajemen inventaris, user, dan jadwal maintenance |
| **Teknisi** | `elyas` | `Password123!` | Pengerjaan checklist PM, submission bukti kerja |
| **Approver (Leader)** | `franz` | `Password123!` | Approval hasil PM & serah terima |
| **Approver (Section Head)** | `eddi` | `Password123!` | Final sign-off maintenance report |

*(Pada halaman login Asset Track juga disediakan tombol Quick-Login demo: Klik nama user untuk langsung masuk).*

---

## 🛠️ Verifikasi Bahwa Setup Berhasil

1. Buka browser ke `http://localhost:5000`:
   - Login dengan `admin_infrahub` / `AdminPass123!`.
   - Pastikan halaman BTS Dashboard terbuka tanpa error, widget status dan chart tampil.
   - Buka menu **UPS Dashboard** (`http://localhost:5000/Ups/Dashboard`).
   - Buka menu **Fleet Tracking** (Login sebagai `tracking_operator` di `http://localhost:5000/Tracking/Live`).
2. Buka browser ke `http://localhost:5001`:
   - Login dengan klik tombol demo `admin` atau `elyas`.
   - Pastikan daftar laptop & PC muncul dengan status Online/Offline.
3. Cek console data generator:
   - Harus terdapat log berkala: `[Monitoring Hub] Injeksi 76 log telemetri UPS baru.`

---

## ❓ FAQ & Troubleshooting

### Q: Muncul error `Login timeout expired` atau `Cannot connect to SQL Server`?
- **Penyebab:** Service SQL Server lokal belum menyala, atau port 1433 belum terbuka.
- **Solusi:** Jalankan `net start MSSQLSERVER`. Cek connection string di `InfraHub.Web/appsettings.json` dan `ITAssetTrack/ITAssetTrack.Web/appsettings.json`. Defaultnya mengarah ke:
  `Server=localhost,1433;Database=...;User Id=sa;Password=Admin123#;TrustServerCertificate=True;`

### Q: Port 5000 atau 5001 sudah terpakai oleh aplikasi lain?
- **Solusi:** Anda dapat mengubah port saat menjalankan perintah, contoh:
  `dotnet run --urls="http://localhost:5005"`
  Pastikan mengupdate port saat membuka browser.
