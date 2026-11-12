# InfraHub Internship Systems (Self-Hosted Hybrid Environment)
Repository proyek magang InfraHub & ITAssetTrack Enterprise: **InfraHub** & **ITAssetTrack (ITAssetTrack)**.

Arsitektur yang digunakan adalah **Hybrid**:
- **Database (MSSQL Server 2022) & Python Dummy Generator:** Berjalan di dalam **Docker Container**.
- **Web Applications (.NET 8 MVC):** Berjalan di lingkungan **Lokal Laptop** (`dotnet run`).

---

## 🚀 Panduan Memulai Cepat (Quick Start)

> 📖 **Panduan Lengkap Langkah demi Langkah**: Lihat file [PANDUAN_SETUP_DAN_MENJALANKAN_PROJECT.md](PANDUAN_SETUP_DAN_MENJALANKAN_PROJECT.md).

### Pilihan Setup Database:
1. **Opsi A (SQL Server Lokal Laptop - Native):**
   - Jalankan `restore_local_sqlserver.bat` (Restore database native).
   - Jalankan `start_generator_local.bat` (Opsional: Injeksi data telemetri dummy).
   - Jalankan `start_local_apps.bat` (Menjalankan 2 aplikasi .NET).

2. **Opsi B (Docker Container):**
   - Jalankan `start_docker_infra.bat` (atau `docker compose up -d`).
   - Jalankan `start_local_apps.bat` (Menjalankan 2 aplikasi .NET).

#### Langkah 2: Jalankan Aplikasi Web .NET (Lokal)
Klik ganda file **`start_local_apps.bat`** atau jalankan di dua terminal:

**Terminal 1 (Monitoring Hub):**
```bash
cd InfraHub.Web
dotnet run --urls="http://localhost:5000"
```

**Terminal 2 (Asset Track):**
```bash
cd ITAssetTrack/ITAssetTrack.Web
dotnet run --urls="http://localhost:5001"
```

---

## 🔑 Akses Web & Kredensial Login

| Aplikasi | URL Akses | Akun & Password Default | Keterangan |
| :--- | :--- | :--- | :--- |
| **InfraHub** | `http://localhost:5000` | User: `admin_infrahub`<br>Pass: `AdminPass123!` | Monitoring BTS, UPS, PAC, dan Tracking armada. *(Tersedia akun lain: `noc_operator`, `tracking_operator`).* |
| **ITAssetTrack** | `http://localhost:5001` | User: `admin`<br>Pass: `Password123!` | Inventaris laptop/PC & Scheduled PM. *(Tersedia akun lain: `elyas` (Teknisi), `franz` (Approver)).* |

---

## 📂 Struktur Repositori

```
├── database/
│   ├── init-scripts/                 # SQL Master Data & Stored Procedures (<7 MB)
│   │   ├── 01_init_databases.sql
│   │   ├── 02_restore_itassettrack.sql
│   │   ├── 03_restore_monitoring_master.sql
│   │   └── 04_create_procedures_monitoring.sql
│   └── run_init.sh                   # Script otomasi restore di container
├── data-generator/                   # Python Service penyuplai telemetri dummy
│   ├── Dockerfile
│   ├── requirements.txt
│   └── generator.py
├── InfraHub.Web/ # Source code ASP.NET Core Monitoring Hub
├── ITAssetTrack/                          # Source code ASP.NET Core ITAssetTrack
├── docker-compose.yml                # Konfigurasi Docker MSSQL & Generator
├── start_docker_infra.bat            # 1-Klik mulai database & generator
├── start_local_apps.bat              # 1-Klik jalankan kedua web lokal
└── README.md
```

---

## 🛠️ Perintah Tambahan yang Berguna

- **Melihat log data generator telemetri:**
  ```bash
  docker logs -f infrahub-data-generator
  ```
- **Menghentikan database container:**
  ```bash
  docker compose down
  ```
- **Reset database ke kondisi awal:**
  ```bash
  docker compose down -v
  docker compose up -d --build
  ```
