import socket
import json
import threading
import uuid
import re
import os
import ssl
import ipaddress
import datetime
from http.server import HTTPServer, BaseHTTPRequestHandler
from datetime import datetime

# Pastikan output console langsung ter-flush seketika (Line Buffering)
try:
    sys.stdout.reconfigure(line_buffering=True)
except Exception:
    pass

HOST = '0.0.0.0'
RAW_PORT = 8080
HTTPS_PORT = 8443

CERT_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'server.crt')
KEY_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'server.key')

def ensure_tls_certificates():
    """Memastikan sertifikat TLS server.crt dan server.key ada, jika belum ada buat otomatis."""
    if os.path.exists(CERT_FILE) and os.path.exists(KEY_FILE):
        return True
    try:
        from cryptography import x509
        from cryptography.x509.oid import NameOID
        from cryptography.hazmat.primitives import hashes, serialization
        from cryptography.hazmat.primitives.asymmetric import rsa

        key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        subject = issuer = x509.Name([
            x509.NameAttribute(NameOID.COUNTRY_NAME, u'ID'),
            x509.NameAttribute(NameOID.ORGANIZATION_NAME, u'InfraHub & ITAssetTrack Enterprise'),
            x509.NameAttribute(NameOID.COMMON_NAME, u'ITAssetTrack Hub'),
        ])
        cert = x509.CertificateBuilder().subject_name(
            subject
        ).issuer_name(
            issuer
        ).public_key(
            key.public_key()
        ).serial_number(
            x509.random_serial_number()
        ).not_valid_before(
            datetime.now(datetime.timezone.utc)
        ).not_valid_after(
            datetime.now(datetime.timezone.utc) + datetime.timedelta(days=3650)
        ).add_extension(
            x509.SubjectAlternativeName([
                x509.DNSName(u'localhost'),
                x509.IPAddress(ipaddress.ip_address('127.0.0.1')),
                x509.IPAddress(ipaddress.ip_address('10.11.3.200')),
            ]),
            critical=False,
        ).sign(key, hashes.SHA256())

        with open(KEY_FILE, 'wb') as f:
            f.write(key.private_bytes(
                encoding=serialization.Encoding.PEM,
                format=serialization.PrivateFormat.TraditionalOpenSSL,
                encryption_algorithm=serialization.NoEncryption(),
            ))
        with open(CERT_FILE, 'wb') as f:
            f.write(cert.public_bytes(serialization.Encoding.PEM))
        print("[+] Generated self-signed TLS certificates for HTTPS server successfully.")
        return True
    except Exception as e:
        print(f"[!] Warning: Gagal membuat sertifikat TLS ({e})")
        return False

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

def save_evidence_file(payload, client_ip):
    """Menyimpan seluruh payload mentah ke disk server sebagai bukti audit fisik (Evidence Store)."""
    try:
        services_dir = os.path.dirname(os.path.abspath(__file__))
        evidence_root = os.path.join(services_dir, 'evidence_reports')
        now = datetime.now()
        month_dir = os.path.join(evidence_root, now.strftime('%Y-%m'))
        os.makedirs(month_dir, exist_ok=True)

        user_clean = re.sub(r'[^a-zA-Z0-9_-]', '_', payload.get('logged_in_user', 'unknown_user'))
        sn_clean = re.sub(r'[^a-zA-Z0-9_-]', '_', payload.get('serial_number', 'unknown_sn'))
        rep_type = re.sub(r'[^a-zA-Z0-9_-]', '_', payload.get('report_type', 'report'))
        ts = now.strftime('%Y%m%d_%H%M%S')

        filename = f"{user_clean}_{sn_clean}_{ts}_{rep_type}.json"
        filepath = os.path.join(month_dir, filename)

        evidence_data = {
            "_meta": {
                "received_at": now.strftime("%Y-%m-%d %H:%M:%S"),
                "client_ip": client_ip,
                "server_version": "1.2.0"
            },
            "payload": payload
        }

        with open(filepath, 'w', encoding='utf-8') as f:
            json.dump(evidence_data, f, indent=2, ensure_ascii=False)
        return filepath
    except Exception as e:
        print(f"[!] Warning: Gagal menyimpan file evidence ({e})")
        return None

def save_to_database(payload):
    """Menyimpan data telemetri agent ke SQL Server db_itassettrack (Skema itassettrack)."""
    sn = payload.get("serial_number", "").strip()
    hostname = payload.get('hostname', '').strip()
    logged_user = payload.get('logged_in_user', '').strip()
    display_name = payload.get('display_name', '').strip()
    
    # Handling Fallback jika SN Unknown
    if not sn or sn.lower() in ["unknown", "to be filled by o.e.m.", "default string", "n/a", "none", "0"]:
        if hostname:
            sn = f"SN-{hostname}"
        else:
            print("[!] Laporan diabaikan: Serial Number & Hostname tidak valid.")
            return {"success": False, "error": "Invalid SN & Hostname"}

    conn = get_db_connection()
    if not conn:
        print("[!] DB Connection offline. Laporan agent hanya di-print ke console.")
        return {"success": False, "error": "DB offline"}

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
        
        # Network Usage (Daily / 24 Hours)
        daily_net = payload.get('daily_network_usage') or net.get('daily_usage') or {}
        net_down_mb = daily_net.get('download_mb')
        net_up_mb = daily_net.get('upload_mb')
        net_total_mb = daily_net.get('total_mb')
        net_usage_text = daily_net.get('text')
        
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

        # Standardize report_type & schedule_triggered
        rep_type = payload.get('report_type', 'Scheduled')
        if rep_type == 'Scheduled Summary':
            rep_type = 'Scheduled'
            
        sched_trig = payload.get('schedule_triggered')
        if sched_trig in ('', '-', 'None', None) or str(sched_trig).startswith('manual_') or str(sched_trig) == 'orphan_recovery':
            sched_trig = None

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
                    Id, AssetId, SerialNumber, Hostname, LoggedInUser, DisplayName, AssignedNrp, IsUserMismatch,
                    OsVersion, ReportType, ScheduleTriggered, RecordIntervalSeconds, PeriodStart, PeriodEnd,
                    CpuUptimeSeconds, BatteryHealthPct, LocalIp, Ssid, Latitude, Longitude,
                    CpuMinPct, CpuAvgPct, CpuMaxPct, RamMinPct, RamAvgPct, RamMaxPct, GpuMinPct, GpuAvgPct, GpuMaxPct,
                    DailyNetworkDownloadMb, DailyNetworkUploadMb, DailyNetworkTotalMb, DailyNetworkUsageText,
                    CreatedAt
                ) VALUES (
                    ?, ?, ?, ?, ?, ?, ?, ?,
                    ?, ?, ?, ?, ?, ?,
                    ?, ?, ?, ?, ?, ?,
                    ?, ?, ?, ?, ?, ?, ?, ?, ?,
                    ?, ?, ?, ?,
                    ?
                )
            """, (
                report_id, asset_id, matched_sn, hostname, logged_user, display_name, assigned_nrp, is_mismatch,
                os_ver, rep_type, sched_trig,
                record_interval, period_start_dt, period_end_dt,
                uptime_sec, bat_health, local_ip, ssid, lat, lng,
                cpu.get('min', 0), cpu.get('avg', 0), cpu.get('max', 0),
                ram.get('min', 0), ram.get('avg', 0), ram.get('max', 0),
                gpu.get('min', 0), gpu.get('avg', 0), gpu.get('max', 0),
                net_down_mb, net_up_mb, net_total_mb, net_usage_text,
                now
            ))

            # Insert Detail Top Apps (Summary)
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

            # Insert Detail App Timeline (Start Time, End Time, Duration)
            app_timeline = payload.get('app_timeline', [])
            for item in app_timeline:
                cursor.execute("""
                    INSERT INTO itassettrack.tbl_t_agent_app_timelines (
                        Id, ReportId, SerialNumber, AppName, StartTime, EndTime, DurationMinutes, IsAFK, CreatedAt
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                """, (
                    str(uuid.uuid4()), report_id, matched_sn, str(item.get('app_name', ''))[:255],
                    str(item.get('start_time', ''))[:30], str(item.get('end_time', ''))[:30],
                    float(item.get('duration_minutes', 0)), 1 if item.get('is_afk') else 0, now
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

            # Insert Installed Software (Inventaris Aplikasi - Bersihkan snapshot lama agar tidak redundan)
            installed_sw = payload.get('installed_software', [])
            if installed_sw and isinstance(installed_sw, list) and len(installed_sw) > 0:
                cursor.execute("DELETE FROM itassettrack.tbl_t_agent_installed_software WHERE SerialNumber = ?", (matched_sn,))
                for sw in installed_sw:
                    sw_name = str(sw.get('name', ''))[:255]
                    sw_ver = str(sw.get('version', ''))[:100] if sw.get('version') else None
                    sw_pub = str(sw.get('publisher', ''))[:255] if sw.get('publisher') else None
                    sw_date = str(sw.get('install_date', ''))[:50] if sw.get('install_date') else None
                    if sw_name:
                        cursor.execute("""
                            INSERT INTO itassettrack.tbl_t_agent_installed_software (
                                Id, ReportId, SerialNumber, SoftwareName, SoftwareVersion, Publisher, InstallDate, CreatedAt
                            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                        """, (
                            str(uuid.uuid4()), report_id, matched_sn, sw_name, sw_ver, sw_pub, sw_date, now
                        ))

            # Insert Login Sessions
            login_sessions = payload.get('login_sessions', [])
            for sess in login_sessions:
                sess_start = parse_datetime(sess.get('login_time'))
                sess_end = parse_datetime(sess.get('last_seen'))
                sess_duration = sess.get('duration_minutes', 0)
                cursor.execute("""
                    INSERT INTO itassettrack.tbl_t_agent_login_sessions (
                        Id, ReportId, SerialNumber, Hostname, LoggedInUser, DisplayName,
                        SessionStart, SessionEnd, DurationMinutes, CreatedAt
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """, (
                    str(uuid.uuid4()), report_id, matched_sn, hostname,
                    str(sess.get('username', ''))[:100], str(sess.get('display_name', ''))[:150],
                    sess_start, sess_end, int(sess_duration), now
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
            return {"success": True, "asset_sn": matched_sn, "status": "registered"}

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
            return {"success": True, "asset_sn": sn, "status": "unregistered"}

    except Exception as e:
        print(f"[-] Error saat query/insert ke SQL Server db_itassettrack: {e}")
        try:
            conn.rollback()
        except Exception:
            pass
        return {"success": False, "error": str(e)}
    finally:
        try:
            conn.close()
        except Exception:
            pass

def process_telemetry_payload(payload, client_ip):
    """Mencetak ringkasan ke terminal dan menyimpannya ke database serta evidence file."""
    rep_type = payload.get("report_type", "Scheduled")
    raw_sched = payload.get('schedule_triggered')
    
    # Standarisasi tipe laporan
    if rep_type == "Scheduled Summary":
        if raw_sched and str(raw_sched).startswith("manual_"):
            rep_type = "Manual Sync"
            raw_sched = None
        elif raw_sched == "orphan_recovery":
            rep_type = "Orphan Recovery"
            raw_sched = None
        else:
            rep_type = "Scheduled"
    elif rep_type in ["Manual Sync", "Orphan Recovery"]:
        raw_sched = None

    if rep_type in ["Scheduled", "Manual Sync", "Orphan Recovery"]:
        # 1. Simpan Evidence JSON ke Folder Server
        evidence_path = save_evidence_file(payload, client_ip)

        net = payload.get('network', {})
        jaringan_str = f"IP: {net.get('local_ip')} | WiFi: {net.get('ssid')}"
        lokasi_str = f"GPS/Coords: {net.get('coordinates')}"
        
        # Network Usage jika dikirimkan (24 Jam / Jadwal Terakhir)
        daily_net = payload.get('daily_network_usage') or net.get('daily_usage')
        usage_str = f" | Usage (24 Jam): {daily_net.get('text')}" if (daily_net and daily_net.get('text')) else ""

        if rep_type == "Manual Sync":
            type_header = "MANUAL SYNC"
        elif rep_type == "Orphan Recovery":
            type_header = "RECOVERY OFFLINE"
        else:
            type_header = f"JADWAL: {raw_sched or '-'}"
        
        print(f"\n[=== LAPORAN SUMMARY | {type_header} | FROM {client_ip} ===]")
        display_nm = payload.get('display_name', '')
        user_str = f"{payload.get('logged_in_user')} ({display_nm})" if display_nm else payload.get('logged_in_user')
        print(f"Host / User : {payload.get('hostname')} / {user_str}")
        print(f"Serial Num. : {payload.get('serial_number')}")
        print(f"OS Version  : {payload.get('os_version')}")
        print(f"Jaringan    : {jaringan_str}{usage_str}")
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
        
        # App Timeline (Start Time, End Time & AFK Details)
        app_timeline = payload.get('app_timeline', [])
        if app_timeline:
            print("Aktivitas Aplikasi & Idle (Timeline Detail):")
            for item in app_timeline:
                afk_tag = " [AFK / IDLE]" if item.get('is_afk') else ""
                print(f"  > {item.get('start_time')} - {item.get('end_time')} | {item.get('app_name')}{afk_tag} ({item.get('duration_minutes', 0)} menit)")

        # Top Apps Summary
        top_apps = payload.get('top_apps', {})
        record_interval = payload.get('record_interval_seconds', 300) 
        if top_apps:
            print("Top Apps Summary:")
            sorted_apps = sorted(top_apps.items(), key=lambda item: item[1], reverse=True)
            for app, count in sorted_apps:
                estimasi_waktu = round(count * (record_interval / 60.0), 2)
                print(f"  - {app} : terekam {count}x (Est. {estimasi_waktu} menit)")

        # Login Sessions
        login_sessions = payload.get('login_sessions', [])
        if login_sessions:
            print("Histori Login Sessions:")
            for sess in login_sessions:
                dn = f" ({sess.get('display_name')})" if sess.get('display_name') else ""
                print(f"  > {sess.get('login_time')} - {sess.get('last_seen')} | {sess.get('username')}{dn} | {sess.get('duration_minutes', 0)} menit")

        # Installed Software Inventory (End of Day / Manual Sync)
        installed_sw = payload.get('installed_software')
        if installed_sw and isinstance(installed_sw, list):
            print(f"Inventaris Software : {len(installed_sw)} aplikasi/software terinstall terdeteksi")
            
        if evidence_path:
            print(f"Evidence File: {evidence_path}")
        print("-" * 75)

        # Update standardized values into payload
        payload['report_type'] = rep_type
        payload['schedule_triggered'] = raw_sched

        # Simpan ke SQL Server db_itassettrack (itassettrack schema)
        return save_to_database(payload)
    else:
        print(f"[!] Menerima format data tidak dikenal dari {client_ip}: {payload.get('report_type')}")
        return {"success": False, "error": "Unknown report type"}

# --- [ HTTPS REST API HANDLER ] ---

class EendsoHTTPSHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        # Override agar log internal HTTP tidak spamming terminal kecuali error
        pass

    def do_GET(self):
        if self.path in ['/api/health', '/health', '/']:
            resp = {
                "status": "healthy",
                "service": "ITAssetTrack Telemetry Hub (HTTPS TLS 1.3)",
                "timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S")
            }
            body = json.dumps(resp).encode('utf-8')
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.send_header('Content-Length', str(len(body)))
            self.send_header('Access-Control-Allow-Origin', '*')
            self.end_headers()
            self.wfile.write(body)
        else:
            self.send_response(404)
            self.send_header('Content-Length', '0')
            self.end_headers()

    def do_POST(self):
        if self.path in ['/api/telemetry/report', '/api/report', '/report']:
            try:
                content_length = int(self.headers.get('Content-Length', 0))
                if content_length <= 0:
                    body = b'{"status":"error","message":"Empty body"}'
                    self.send_response(400)
                    self.send_header('Content-Type', 'application/json')
                    self.send_header('Content-Length', str(len(body)))
                    self.end_headers()
                    self.wfile.write(body)
                    return

                post_data = self.rfile.read(content_length)
                payload = json.loads(post_data.decode('utf-8', errors='ignore'))
                
                client_ip = self.client_address[0]
                result = process_telemetry_payload(payload, client_ip)

                if result and result.get("success"):
                    resp = {
                        "status": "success",
                        "message": "Telemetry report accepted and saved",
                        "asset_sn": result.get("asset_sn", ""),
                        "server_time": datetime.now().strftime("%Y-%m-%d %H:%M:%S")
                    }
                    body = json.dumps(resp).encode('utf-8')
                    self.send_response(200)
                    self.send_header('Content-Type', 'application/json')
                    self.send_header('Content-Length', str(len(body)))
                    self.end_headers()
                    self.wfile.write(body)
                else:
                    resp = {
                        "status": "error",
                        "message": result.get("error", "Database error"),
                        "server_time": datetime.now().strftime("%Y-%m-%d %H:%M:%S")
                    }
                    body = json.dumps(resp).encode('utf-8')
                    self.send_response(500)
                    self.send_header('Content-Type', 'application/json')
                    self.send_header('Content-Length', str(len(body)))
                    self.end_headers()
                    self.wfile.write(body)

            except Exception as e:
                resp = {"status": "error", "message": str(e)}
                body = json.dumps(resp).encode('utf-8')
                self.send_response(500)
                self.send_header('Content-Type', 'application/json')
                self.send_header('Content-Length', str(len(body)))
                self.end_headers()
                self.wfile.write(body)
        else:
            self.send_response(404)
            self.send_header('Content-Length', '0')
            self.end_headers()

def run_https_server():
    """Menjalankan HTTPS REST API Server di Port 8443 (TLS Encrypted)."""
    ensure_tls_certificates()
    httpd = HTTPServer((HOST, HTTPS_PORT), EendsoHTTPSHandler)
    
    ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    ctx.load_cert_chain(certfile=CERT_FILE, keyfile=KEY_FILE)
    httpd.socket = ctx.wrap_socket(httpd.socket, server_side=True)
    
    print(f"[*] [HTTPS] ITAssetTrack Encrypted API Server berjalan di https://{HOST}:{HTTPS_PORT} (TLS Encrypted)")
    httpd.serve_forever()

def handle_legacy_socket_client(conn, addr):
    """Menerima chunk data dari raw TCP socket legacy jika ada."""
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
            process_telemetry_payload(payload, addr[0])
                
        except Exception as e:
            print(f"[-] Error socket {addr[0]}: {e}")

def run_raw_socket_server():
    """Menjalankan Raw Socket Server di Port 8080 untuk backward-compatibility."""
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            s.bind((HOST, RAW_PORT))
            s.listen()
            print(f"[*] [RAW TCP] Legacy Socket Server berjalan di Port {RAW_PORT}...")
            
            while True:
                conn, addr = s.accept()
                threading.Thread(target=handle_legacy_socket_client, args=(conn, addr), daemon=True).start()
    except Exception as e:
        print(f"[!] Warning Raw Socket Port {RAW_PORT} tidak dapat dibuka ({e})")

def start_server():
    print(f"==========================================================================")
    print(f"  InfraHub ITAssetTrack Hub Server (SOC Compliant - TLS Encrypted)")
    print(f"  Target Database: db_itassettrack (Skema: itassettrack)")
    print(f"==========================================================================\n")
    
    # 1. Start Raw TCP Socket Server di background thread
    raw_thread = threading.Thread(target=run_raw_socket_server, daemon=True)
    raw_thread.start()
    
    # 2. Start HTTPS Server di main thread
    run_https_server()

if __name__ == "__main__":
    start_server()
