import socket
import json
import threading
import uuid
import re
from datetime import datetime

HOST = '0.0.0.0'
PORT = 8080

def format_uptime(seconds):
    if not seconds:
        return "0 Hari, 0 Jam, 0 Menit"
    days = seconds // 86400
    hours = (seconds % 86400) // 3600
    minutes = (seconds % 3600) // 60
    return f"{days} Hari, {hours} Jam, {minutes} Menit"

def parse_datetime(dt_val):
    if not dt_val:
        return datetime.now()
    if isinstance(dt_val, datetime):
        return dt_val
    try:
        clean_str = str(dt_val).replace('T', ' ').split('.')[0]
        return datetime.strptime(clean_str, "%Y-%m-%d %H:%M:%S")
    except Exception:
        return datetime.now()

def get_db_connection():
    """Membuka koneksi ke SQL Server asset_sysdv401.infrahub.net,1433 / db_itassettrack (skema itassettrack)."""
    # 1. Coba pyodbc Driver 17
    try:
        import pyodbc
        conn_str = (
            "DRIVER={ODBC Driver 17 for SQL Server};"
            "SERVER=asset_sysdv401.infrahub.net,1433;"
            "DATABASE=db_itassettrack;"
            "UID=user_infra_dept_app;"
            "PWD=infrahub.123;"
            "TrustServerCertificate=yes;"
        )
        return pyodbc.connect(conn_str, timeout=10)
    except Exception:
        pass

    # 2. Coba pyodbc Driver 18
    try:
        import pyodbc
        conn_str = (
            "DRIVER={ODBC Driver 18 for SQL Server};"
            "SERVER=asset_sysdv401.infrahub.net,1433;"
            "DATABASE=db_itassettrack;"
            "UID=user_infra_dept_app;"
            "PWD=infrahub.123;"
            "TrustServerCertificate=yes;"
        )
        return pyodbc.connect(conn_str, timeout=10)
    except Exception:
        pass

    # 3. Coba pymssql
    try:
        import pymssql
        return pymssql.connect(
            server='asset_sysdv401.infrahub.net',
            port=1433,
            user='user_infra_dept_app',
            password='infrahub.123',
            database='db_itassettrack',
            login_timeout=10
        )
    except Exception as e:
        print(f"[!] Warning: Gagal terhubung ke DB SQL Server db_itassettrack ({e})")
        return None

def parse_coordinates(coords_str):
    """Ekstrak latitude & longitude decimal dari string '-6.1983,106.9153'."""
    if not coords_str or coords_str == "Unknown":
        return None, None
    try:
        parts = coords_str.split(',')
        if len(parts) == 2:
            lat = float(parts[0].strip())
            lng = float(parts[1].strip())
            return lat, lng
    except Exception:
        pass
    return None, None

def save_to_database(payload):
    """Menyimpan data telemetri agent ke SQL Server db_itassettrack (Skema itassettrack)."""
    sn = payload.get("serial_number", "").strip()
    hostname = payload.get('hostname', '').strip()
    logged_user = payload.get('logged_in_user', '').strip()
    
    # Handling Fallback jika SN Unknown
    if not sn or sn.lower() in ["unknown", "to be filled by o.e.m.", "default string", "n/a", "none", "0"]:
        if hostname:
            sn = f"SN-{hostname}"
        else:
            print("[!] Laporan diabaikan: Serial Number & Hostname tidak valid.")
            return

    conn = get_db_connection()
    if not conn:
        print("[!] DB Connection offline. Laporan agent hanya di-print ke console.")
        return

    try:
        cursor = conn.cursor()
        
        # 1. Cek apakah asset terdaftar di itassettrack.tbl_t_asset berdasarkan SerialNumber atau ComputerName
        cursor.execute("""
            SELECT Id, SerialNumber 
            FROM itassettrack.tbl_t_asset 
            WHERE SerialNumber = ? OR ComputerName = ?
        """, (sn, hostname))
        row = cursor.fetchone()
        
        net = payload.get('network', {})
        local_ip = net.get('local_ip', '')
        ssid = net.get('ssid', '')
        coords = net.get('coordinates', '')
        lat, lng = parse_coordinates(coords)
        
        os_ver = payload.get('os_version', '')
        period_start_dt = parse_datetime(payload.get('period_start'))
        period_end_dt = parse_datetime(payload.get('period_end'))
        uptime_sec = payload.get('cpu_uptime_seconds', 0)
        bat_health = payload.get('battery_health_pct')
        record_interval = payload.get('record_interval_seconds', 300)

        hw = payload.get('hardware_stats', {})
        cpu = hw.get('cpu', {})
        ram = hw.get('ram', {})
        gpu = hw.get('gpu', {})
        
        now = datetime.now()

        if row:
            # === A. LAPTOP TERDAFTAR (REGISTERED) ===
            asset_id = row[0]
            matched_sn = row[1] or sn
            
            # Cek penugasan karyawan untuk deteksi User Mismatch Alert
            assigned_nrp = ""
            cursor.execute("""
                SELECT TOP 1 e.NRP 
                FROM itassettrack.tbl_r_asset_assignment aa
                INNER JOIN itassettrack.tbl_m_employee e ON aa.EmployeeId = e.Id
                WHERE aa.AssetId = ? AND aa.IsActive = 1
                ORDER BY aa.CreatedAt DESC
            """, (asset_id,))
            assign_row = cursor.fetchone()
            if assign_row and assign_row[0]:
                assigned_nrp = assign_row[0]

            is_mismatch = 1 if (assigned_nrp and logged_user and assigned_nrp.lower() != logged_user.lower()) else 0

            # Insert Header Report
            report_id = str(uuid.uuid4())
            cursor.execute("""
                INSERT INTO itassettrack.tbl_t_agent_reports (
                    Id, AssetId, SerialNumber, Hostname, LoggedInUser, AssignedNrp, IsUserMismatch,
                    OsVersion, ReportType, ScheduleTriggered, RecordIntervalSeconds, PeriodStart, PeriodEnd,
                    CpuUptimeSeconds, BatteryHealthPct, LocalIp, Ssid, Latitude, Longitude,
                    CpuMinPct, CpuAvgPct, CpuMaxPct, RamMinPct, RamAvgPct, RamMaxPct, GpuMinPct, GpuAvgPct, GpuMaxPct,
                    CreatedAt
                ) VALUES (
                    ?, ?, ?, ?, ?, ?, ?,
                    ?, ?, ?, ?, ?, ?,
                    ?, ?, ?, ?, ?, ?,
                    ?, ?, ?, ?, ?, ?, ?, ?, ?,
                    ?
                )
            """, (
                report_id, asset_id, matched_sn, hostname, logged_user, assigned_nrp, is_mismatch,
                os_ver, payload.get('report_type', 'Scheduled Summary'), payload.get('schedule_triggered', ''),
                record_interval, period_start_dt, period_end_dt,
                uptime_sec, bat_health, local_ip, ssid, lat, lng,
                cpu.get('min', 0), cpu.get('avg', 0), cpu.get('max', 0),
                ram.get('min', 0), ram.get('avg', 0), ram.get('max', 0),
                gpu.get('min', 0), gpu.get('avg', 0), gpu.get('max', 0),
                now
            ))

            # Insert Detail Top Apps
            top_apps = payload.get('top_apps', {})
            top_app_is_afk = False
            if top_apps:
                sorted_apps = sorted(top_apps.items(), key=lambda item: item[1], reverse=True)
                if sorted_apps and ("AFK" in sorted_apps[0][0] or "Idle" in sorted_apps[0][0]):
                    top_app_is_afk = True

                for app_name, count in top_apps.items():
                    est_min = round(count * (record_interval / 60.0), 2)
                    is_afk = 1 if ("AFK" in app_name or "Idle" in app_name) else 0
                    cursor.execute("""
                        INSERT INTO itassettrack.tbl_t_agent_top_apps (
                            Id, ReportId, SerialNumber, AppName, RecordCount, EstimatedDurationMinutes, IsAFK, PeriodStart
                        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                    """, (
                        str(uuid.uuid4()), report_id, matched_sn, str(app_name)[:255], count, est_min, is_afk, period_start_dt
                    ))

            # Insert Detail Battery Timeline
            battery_events = payload.get('battery_timeline', [])
            for ev in battery_events:
                cursor.execute("""
                    INSERT INTO itassettrack.tbl_t_agent_battery_timelines (
                        Id, ReportId, SerialNumber, Status, StartTime, EndTime, StartPct, EndPct
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """, (
                    str(uuid.uuid4()), report_id, matched_sn, str(ev.get('status', ''))[:50],
                    str(ev.get('start_time', ''))[:20], str(ev.get('end_time', ''))[:20],
                    ev.get('start_pct', 0), ev.get('end_pct', 0)
                ))

            # Update Telemetry Status pada itassettrack.tbl_t_asset
            active_apps_summary = ", ".join(list(top_apps.keys())[:3]) if top_apps else ""
            cursor.execute("""
                UPDATE itassettrack.tbl_t_asset
                SET ComputerName = ?,
                    OperatingSystem = ?,
                    LastOnlineTime = ?,
                    Uptime = ?,
                    IsAFK = ?,
                    ActiveApps = ?,
                    LastIp = ?,
                    LastLoggedInUser = ?,
                    LastBatteryHealthPct = ?,
                    IsOnline = 1,
                    IsUserMismatch = ?,
                    UpdatedAt = ?
                WHERE Id = ?
            """, (
                hostname, os_ver, period_end_dt, format_uptime(uptime_sec),
                1 if top_app_is_afk else 0, active_apps_summary[:255],
                local_ip, logged_user, bat_health, is_mismatch, now, asset_id
            ))

            conn.commit()
            print(f"[OK] Berhasil menyimpan laporan telemetri terdaftar ke DB db_itassettrack (Asset SN: {matched_sn})")

        else:
            # === B. LAPTOP BELUM TERDAFTAR (UNREGISTERED) ===
            cursor.execute("SELECT Id, ReportCount FROM itassettrack.tbl_s_unregistered_assets WHERE SerialNumber = ?", (sn,))
            unreg_row = cursor.fetchone()

            if unreg_row:
                unreg_id = unreg_row[0]
                new_count = (unreg_row[1] or 0) + 1
                cursor.execute("""
                    UPDATE itassettrack.tbl_s_unregistered_assets
                    SET Hostname = ?, LoggedInUser = ?, OsVersion = ?, LocalIp = ?, Ssid = ?,
                        Latitude = ?, Longitude = ?, LastSeenAt = ?, ReportCount = ?, UpdatedAt = ?
                    WHERE Id = ?
                """, (
                    hostname, logged_user, os_ver, local_ip, ssid, lat, lng, period_end_dt, new_count, now, unreg_id
                ))
            else:
                cursor.execute("""
                    INSERT INTO itassettrack.tbl_s_unregistered_assets (
                        Id, SerialNumber, Hostname, LoggedInUser, OsVersion, LocalIp, Ssid,
                        Latitude, Longitude, LastSeenAt, ReportCount, Status, CreatedAt
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1, 'Pending Review', ?)
                """, (
                    str(uuid.uuid4()), sn, hostname, logged_user, os_ver, local_ip, ssid,
                    lat, lng, period_end_dt, now
                ))

            conn.commit()
            print(f"[!] Perangkat belum terdaftar ({sn}). Berhasil dicatat ke itassettrack.tbl_s_unregistered_assets untuk diaudit.")

    except Exception as e:
        print(f"[-] Error saat query/insert ke SQL Server db_itassettrack: {e}")
        try:
            conn.rollback()
        except Exception:
            pass
    finally:
        try:
            conn.close()
        except Exception:
            pass

def handle_client(conn, addr):
    """Menerima seluruh chunk data dari socket client (agent_service.exe) secara utuh."""
    with conn:
        try:
            conn.settimeout(15)
            chunks = []
            while True:
                try:
                    chunk = conn.recv(16384)
                    if not chunk:
                        break
                    chunks.append(chunk)
                except socket.timeout:
                    break

            if not chunks:
                return

            raw_bytes = b"".join(chunks)
            payload = json.loads(raw_bytes.decode('utf-8', errors='ignore'))
            
            if payload.get("report_type") == "Scheduled Summary":
                net = payload.get('network', {})
                jaringan_str = f"IP: {net.get('local_ip')} | WiFi: {net.get('ssid')}"
                lokasi_str = f"GPS/Coords: {net.get('coordinates')}"
                
                print(f"\n[=== LAPORAN SUMMARY | JADWAL: {payload.get('schedule_triggered')} | FROM {addr[0]} ===]")
                print(f"Host / User : {payload.get('hostname')} / {payload.get('logged_in_user')}")
                print(f"Serial Num. : {payload.get('serial_number')}")
                print(f"OS Version  : {payload.get('os_version')}")
                print(f"Jaringan    : {jaringan_str}")
                print(f"Lokasi      : {lokasi_str}")
                print(f"Periode     : {payload.get('period_start')} to {payload.get('period_end')}")
                print(f"Runtime OS  : {format_uptime(payload.get('cpu_uptime_seconds'))}")
                
                # Statistik Hardware
                hw = payload.get('hardware_stats', {})
                if hw:
                    print("Statistik Hardware (Low / Average / Top):")
                    print(f"  - CPU  : {hw.get('cpu',{}).get('min')}% / {hw.get('cpu',{}).get('avg')}% / {hw.get('cpu',{}).get('max')}%")
                    print(f"  - RAM  : {hw.get('ram',{}).get('min')}% / {hw.get('ram',{}).get('avg')}% / {hw.get('ram',{}).get('max')}%")
                    print(f"  - VGA  : {hw.get('gpu',{}).get('min')}% / {hw.get('gpu',{}).get('avg')}% / {hw.get('gpu',{}).get('max')}%")

                # Riwayat Baterai
                bat_health = payload.get('battery_health_pct')
                bat_health_str = f"{bat_health}%" if bat_health is not None else "Unknown"
                print(f"Riwayat Baterai (Health Realtime via BMS: {bat_health_str}):")
                battery_events = payload.get('battery_timeline', [])
                if battery_events:
                    for ev in battery_events:
                        status_id = "Cas (Charging)" if ev.get('status') == "Charging" else "Lepas (Discharging)"
                        print(f"  > {ev.get('start_time')} - {ev.get('end_time')} | {status_id} | Dari {ev.get('start_pct')}% ke {ev.get('end_pct')}%")
                
                # Top Apps
                print("Aktivitas Aplikasi (Top Apps):")
                top_apps = payload.get('top_apps', {})
                record_interval = payload.get('record_interval_seconds', 300) 
                if top_apps:
                    sorted_apps = sorted(top_apps.items(), key=lambda item: item[1], reverse=True)
                    for app, count in sorted_apps:
                        estimasi_waktu = round(count * (record_interval / 60.0), 2)
                        print(f"  - {app} : terekam {count}x (Est. {estimasi_waktu} menit)")
                    
                print("-" * 75)

                # Simpan ke SQL Server db_itassettrack (itassettrack schema)
                save_to_database(payload)

            else:
                print(f"[!] Menerima format data tidak dikenal dari {addr[0]}: {payload.get('report_type')}")
                
        except json.JSONDecodeError as e:
            print(f"[!] Gagal membaca JSON dari {addr[0]}. Payload rusak/terpotong: {e}")
        except Exception as e:
            print(f"[-] Error saat memproses data dari {addr[0]}: {e}")

def start_server():
    print(f"[*] InfraHub Eendso Agent Hub Server berjalan di Port {PORT}...")
    print(f"[*] Connected Database Target: db_itassettrack (Skema: itassettrack)")
    print(f"[*] Ready & Listening for agent summary reports...\n")
    
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        s.bind((HOST, PORT))
        s.listen()
        
        while True:
            conn, addr = s.accept()
            client_thread = threading.Thread(target=handle_client, args=(conn, addr))
            client_thread.start()

if __name__ == "__main__":
    start_server()