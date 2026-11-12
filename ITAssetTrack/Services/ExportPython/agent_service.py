import socket
import psutil
import json
import time
import getpass
import subprocess
import ctypes
import sqlite3
import re
import os
import sys
import winreg
import uuid
import threading
from datetime import datetime

# Disable InsecureRequestWarning for internal self-signed TLS certificates
import requests
import urllib3
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

# --- [ KONFIGURASI PRODUKSI (ENTERPRISE - SOC COMPLIANT) ] ---
SERVER_IP = '10.11.3.200'
HTTPS_PORT = 8443
RAW_PORT = 8080

SERVER_HTTPS_URL = f"https://{SERVER_IP}:{HTTPS_PORT}/api/telemetry/report"
SERVER_HEALTH_URL = f"https://{SERVER_IP}:{HTTPS_PORT}/api/health"

RECORD_INTERVAL = 300            # Simpan ke DB Lokal berkala (5 Menit = 300 detik)
AFK_THRESHOLD = 600             # Batas waktu AFK (10 Menit = 600 detik)
BATTERY_HEALTH_INTERVAL = 14400 # Cek Kesehatan Baterai (4 Jam)
SCHEDULE_TIMES = ["07:59", "12:00", "16:24"]
MAX_RETENTION_DAYS = 14         # Batas maksimal simpan data lokal saat offline (14 hari)

# Menentukan folder data aplikasi di ProgramData (Standar Windows Enterprise)
def get_database_dir():
    program_data = os.environ.get('ProgramData', r'C:\ProgramData')
    target_dir = os.path.join(program_data, 'EendsoPulse')
    try:
        os.makedirs(target_dir, exist_ok=True)
        # Test write permission
        test_file = os.path.join(target_dir, '.perm_test')
        with open(test_file, 'w') as f:
            f.write('ok')
        os.remove(test_file)
        return target_dir
    except Exception:
        fallback_dir = os.path.join(os.environ.get('PUBLIC', r'C:\Users\Public'), 'EendsoPulse')
        os.makedirs(fallback_dir, exist_ok=True)
        return fallback_dir

DB_DIR = get_database_dir()
DB_PATH = os.path.join(DB_DIR, 'itassettrack_local.db')

_CACHED_SERIAL_NUMBER = None
_LAST_SYNC_TIME_STR = "Belum Ada"
_TRAY_ICON = None

# --- [ WINDOWS SHUTDOWN & LOGOFF HANDLER ] ---
CTRL_HANDLER_TYPE = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_uint)

def _windows_ctrl_handler(ctrl_type):
    # 0=CTRL_C, 1=CTRL_BREAK, 2=CTRL_CLOSE, 5=CTRL_LOGOFF, 6=CTRL_SHUTDOWN
    if ctrl_type in (2, 5, 6):
        try:
            global _TRAY_ICON
            if _TRAY_ICON:
                _TRAY_ICON.stop()
        except Exception:
            pass
        os._exit(0)
        return True
    return False

_global_ctrl_handler = CTRL_HANDLER_TYPE(_windows_ctrl_handler)
try:
    ctypes.windll.kernel32.SetConsoleCtrlHandler(_global_ctrl_handler, True)
except Exception:
    pass

class LASTINPUTINFO(ctypes.Structure):
    _fields_ = [("cbSize", ctypes.c_uint), ("dwTime", ctypes.c_uint)]

def apply_db_security_acls(db_dir, db_path):
    """
    Mengamankan direktori ProgramData dan file DB agar tidak bisa dimodifikasi bebas
    oleh pengguna non-admin (hanya SYSTEM & Administrators yang berkuasa penuh).
    """
    try:
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        if os.path.exists(db_dir):
            subprocess.run(['icacls', db_dir, '/grant:r', 'SYSTEM:(OI)(CI)F', 'Administrators:(OI)(CI)F', 'Users:(OI)(CI)RX', '/t', '/c'],
                           capture_output=True, startupinfo=startupinfo, timeout=5)
        if os.path.exists(db_path):
            subprocess.run(['icacls', db_path, '/grant:r', 'SYSTEM:F', 'Administrators:F', 'Users:RX', '/c'],
                           capture_output=True, startupinfo=startupinfo, timeout=5)
    except Exception:
        pass

def setup_database():
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute("PRAGMA journal_mode=WAL;")
    
    # Tabel activity_log dengan kolom is_synced untuk jaminan pengiriman (Store-and-Forward)
    c.execute('''CREATE TABLE IF NOT EXISTS activity_log 
                 (timestamp TEXT, username TEXT, battery_percent INT, is_plugged INT, active_window TEXT, 
                  cpu_pct INT, ram_pct INT, gpu_pct INT, bat_health_pct INT, is_synced INT DEFAULT 0)''')
    
    # Migrasi jika kolom is_synced belum ada pada DB lama
    c.execute("PRAGMA table_info(activity_log);")
    columns = [col[1] for col in c.fetchall()]
    if "is_synced" not in columns:
        try:
            c.execute("ALTER TABLE activity_log ADD COLUMN is_synced INT DEFAULT 0;")
        except Exception:
            pass

    # Tabel session_log untuk tracking histori login per-user
    c.execute('''CREATE TABLE IF NOT EXISTS session_log
                 (session_id TEXT PRIMARY KEY, username TEXT, display_name TEXT, 
                  login_time TEXT, last_seen TEXT, is_sent INT DEFAULT 0)''')
    
    # Tabel metadata aplikasi
    c.execute('''CREATE TABLE IF NOT EXISTS app_metadata
                 (key TEXT PRIMARY KEY, value TEXT)''')
                 
    conn.commit()
    conn.close()
    
    apply_db_security_acls(DB_DIR, DB_PATH)

def cleanup_old_retention_data(conn=None):
    """Membersihkan data yang sudah terkirim (is_synced=1) dan data offline > 14 hari (anti-bloat)."""
    close_at_end = False
    if conn is None:
        conn = sqlite3.connect(DB_PATH)
        close_at_end = True
    try:
        c = conn.cursor()
        c.execute("DELETE FROM activity_log WHERE is_synced = 1")
        c.execute("DELETE FROM activity_log WHERE timestamp < datetime('now', '-14 days')")
        c.execute("DELETE FROM session_log WHERE is_sent = 1 AND login_time < datetime('now', '-7 days')")
        conn.commit()
    except Exception:
        pass
    finally:
        if close_at_end:
            conn.close()

def save_to_local(username, battery, is_plugged, window, cpu, ram, gpu, bat_health):
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    plugged_int = 1 if is_plugged else 0
    c.execute("INSERT INTO activity_log (timestamp, username, battery_percent, is_plugged, active_window, cpu_pct, ram_pct, gpu_pct, bat_health_pct, is_synced) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 0)", 
              (now, username, battery, plugged_int, window, cpu, ram, gpu, bat_health))
    conn.commit()
    cleanup_old_retention_data(conn)
    conn.close()

def record_current_snapshot():
    """Mengambil dan menyimpan snapshot aktivitas detik ini secara instan."""
    try:
        user = getpass.getuser()
        bat = psutil.sensors_battery()
        bat_pct = bat.percent if bat else 100
        is_plugged = bat.power_plugged if bat else True
        cpu_val = int(psutil.cpu_percent(interval=0.5))
        ram_val = int(psutil.virtual_memory().percent)
        gpu_val = get_gpu_usage()
        idle_seconds = get_idle_time()
        active_win = "[AFK / Tidak Ada Aktivitas]" if idle_seconds >= AFK_THRESHOLD else get_active_window()
        cached_bat_health = get_battery_health_powercfg()
        save_to_local(user, bat_pct, is_plugged, active_win, cpu_val, ram_val, gpu_val, cached_bat_health)
    except Exception:
        pass

def get_idle_time():
    try:
        user32 = ctypes.windll.user32
        kernel32 = ctypes.windll.kernel32
        lii = LASTINPUTINFO()
        lii.cbSize = ctypes.sizeof(LASTINPUTINFO)
        if user32.GetLastInputInfo(ctypes.byref(lii)):
            tick = kernel32.GetTickCount()
            return int((tick - lii.dwTime) / 1000.0)
    except Exception:
        pass
    return 0

def get_user_display_name():
    """Mengambil Display Name user yang login via Win32 API (e.g., 'Aldo Febryan')"""
    try:
        secur32 = ctypes.windll.secur32
        NameDisplay = 3  # EXTENDED_NAME_FORMAT: NameDisplay
        size = ctypes.pointer(ctypes.c_ulong(0))
        secur32.GetUserNameExW(NameDisplay, None, size)
        name_buf = ctypes.create_unicode_buffer(size.contents.value)
        if secur32.GetUserNameExW(NameDisplay, name_buf, size):
            if name_buf.value:
                return name_buf.value
    except Exception:
        pass
    
    try:
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        ps_cmd = '([adsi]"WinNT://$env:USERDOMAIN/$env:USERNAME,user").FullName'
        out = subprocess.check_output(
            ['powershell', '-NoProfile', '-WindowStyle', 'Hidden', '-Command', ps_cmd],
            startupinfo=startupinfo, timeout=5
        ).decode('utf-8', errors='ignore').strip()
        if out and out != "":
            return out
    except Exception:
        pass
    
    return ""

def get_battery_health_powercfg():
    """Mengambil Battery Health (%) secara aman tanpa meninggalkan file STDOUT di disk"""
    health_pct = None
    
    try:
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        ps_cmd = (
            "$f = (Get-CimInstance -Namespace root/wmi -ClassName BatteryFullChargedCapacity -ErrorAction SilentlyContinue).FullChargedCapacity; "
            "$d = (Get-CimInstance -Namespace root/wmi -ClassName BatteryStaticData -ErrorAction SilentlyContinue).DesignedCapacity; "
            "if ($f -and $d -and $d -gt 0) { [math]::Round(($f / $d) * 100) }"
        )
        out = subprocess.check_output(
            ['powershell', '-NoProfile', '-WindowStyle', 'Hidden', '-Command', ps_cmd],
            startupinfo=startupinfo, timeout=8
        ).decode('utf-8', errors='ignore').strip()
        
        if out and out.isdigit():
            health_pct = min(int(out), 100)
    except Exception:
        pass

    if health_pct is None:
        import tempfile
        temp_xml = os.path.join(tempfile.gettempdir(), 'infrahub_bat_temp.xml')
        try:
            startupinfo = subprocess.STARTUPINFO()
            startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            res = subprocess.run(['powercfg', '/batteryreport', '/xml', '/output', temp_xml], 
                                 capture_output=True, text=True, startupinfo=startupinfo, timeout=10)
            if os.path.exists(temp_xml):
                with open(temp_xml, 'r', encoding='utf-8', errors='ignore') as f:
                    content = f.read()
                design_match = re.search(r'<DesignCapacity>(\d+)</DesignCapacity>', content, re.IGNORECASE)
                full_match = re.search(r'<FullChargeCapacity>(\d+)</FullChargeCapacity>', content, re.IGNORECASE)
                if design_match and full_match:
                    design_cap = float(design_match.group(1))
                    full_cap = float(full_match.group(1))
                    if design_cap > 0:
                        health_pct = min(round((full_cap / design_cap) * 100), 100)
        except Exception:
            pass
        finally:
            if os.path.exists(temp_xml):
                try:
                    os.remove(temp_xml)
                except Exception:
                    pass

    return health_pct

def get_cpu_uptime_seconds():
    return int(time.time() - psutil.boot_time())

def get_gpu_usage():
    try:
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        cmd = 'powershell "Get-Counter \'\\GPU Engine(*engtype_3D)\\Utilization Percentage\' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty CounterSamples | Measure-Object -Property CookedValue -Sum | Select-Object -ExpandProperty Sum"'
        out = subprocess.check_output(cmd, shell=True, timeout=5, startupinfo=startupinfo).decode('utf-8').strip()
        if out: return int(float(out))
    except Exception:
        pass
    return 0

def get_os_version():
    """Membaca OS Name, Edition, DisplayVersion (e.g. 24H2, 23H2), dan Build Number secara akurat."""
    try:
        key = winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\Windows NT\CurrentVersion")
        build = str(winreg.QueryValueEx(key, "CurrentBuild")[0])
        try:
            ubr = str(winreg.QueryValueEx(key, "UBR")[0])
            full_build = f"{build}.{ubr}"
        except Exception:
            full_build = build
        
        display_ver = ""
        try:
            display_ver = str(winreg.QueryValueEx(key, "DisplayVersion")[0]).strip()
        except Exception:
            try:
                display_ver = str(winreg.QueryValueEx(key, "ReleaseId")[0]).strip()
            except Exception:
                pass
        
        # Fallback mapping jika display_ver tidak terdeteksi
        if not display_ver:
            b_int = int(build) if build.isdigit() else 0
            if b_int >= 26100: display_ver = "24H2"
            elif b_int >= 22631: display_ver = "23H2"
            elif b_int >= 22621: display_ver = "22H2"
            elif b_int >= 22000: display_ver = "21H2"
            elif b_int >= 19045: display_ver = "22H2"
            elif b_int >= 19044: display_ver = "21H2"

        prod_name = ""
        try:
            prod_name = str(winreg.QueryValueEx(key, "ProductName")[0]).strip()
        except Exception:
            pass
            
        winreg.CloseKey(key)

        os_base = "Windows 11" if (int(build) if build.isdigit() else 0) >= 22000 else "Windows 10"
        
        edition = ""
        if "Enterprise" in prod_name: edition = " Enterprise"
        elif "Pro" in prod_name: edition = " Pro"
        elif "Home" in prod_name: edition = " Home"
        
        ver_part = f" {display_ver}" if display_ver else ""
        return f"{os_base}{edition}{ver_part} (Build {full_build})"
    except Exception:
        try:
            build = sys.getwindowsversion().build
            os_base = "Windows 11" if build >= 22000 else "Windows 10"
            return f"{os_base} (Build {build})"
        except Exception:
            return "Unknown OS"

def get_daily_network_usage():
    """
    Mengambil data penggunaan kuota jaringan aktual (Total Download & Upload)
    dari adapter jaringan aktif (seperti yang tertera pada Windows Data Usage Settings).
    """
    try:
        net_io = psutil.net_io_counters()
        curr_recv = net_io.bytes_recv
        curr_sent = net_io.bytes_sent

        down_mb = round(curr_recv / (1024.0 * 1024.0), 2)
        up_mb = round(curr_sent / (1024.0 * 1024.0), 2)
        total_mb = round(down_mb + up_mb, 2)

        def format_size(mb_val):
            if mb_val >= 1024:
                return f"{round(mb_val / 1024.0, 2)} GB"
            return f"{mb_val} MB"

        text_str = f"Download: {format_size(down_mb)} | Upload: {format_size(up_mb)} (Total: {format_size(total_mb)})"

        return {
            "download_mb": down_mb,
            "upload_mb": up_mb,
            "total_mb": total_mb,
            "text": text_str
        }
    except Exception:
        try:
            startupinfo = subprocess.STARTUPINFO()
            startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            ps_cmd = "$r = (Get-NetAdapterStatistics | Measure-Object -Property ReceivedBytes -Sum).Sum; $s = (Get-NetAdapterStatistics | Measure-Object -Property SentBytes -Sum).Sum; Write-Host \"$r,$s\""
            out = subprocess.check_output(
                ['powershell', '-NoProfile', '-WindowStyle', 'Hidden', '-Command', ps_cmd],
                startupinfo=startupinfo, timeout=6
            ).decode('utf-8', errors='ignore').strip()
            parts = out.split(',')
            if len(parts) == 2:
                r_bytes = float(parts[0].strip()) if parts[0].strip() else 0
                s_bytes = float(parts[1].strip()) if parts[1].strip() else 0
                down_mb = round(r_bytes / (1024.0 * 1024.0), 2)
                up_mb = round(s_bytes / (1024.0 * 1024.0), 2)
                total_mb = round(down_mb + up_mb, 2)

                def format_size(mb_val):
                    if mb_val >= 1024:
                        return f"{round(mb_val / 1024.0, 2)} GB"
                    return f"{mb_val} MB"

                text_str = f"Download: {format_size(down_mb)} | Upload: {format_size(up_mb)} (Total: {format_size(total_mb)})"
                return {
                    "download_mb": down_mb,
                    "upload_mb": up_mb,
                    "total_mb": total_mb,
                    "text": text_str
                }
        except Exception:
            pass

        return {
            "download_mb": 0.0,
            "upload_mb": 0.0,
            "total_mb": 0.0,
            "text": "Download: 0 MB | Upload: 0 MB (Total: 0 MB)"
        }

def get_installed_software():
    """
    Mengambil daftar seluruh aplikasi dan software yang terinstall di Windows
    melalui Registry (64-bit, 32-bit, dan Current User).
    """
    apps = []
    reg_paths = [
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", winreg.KEY_WOW64_64KEY),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", winreg.KEY_WOW64_32KEY),
        (winreg.HKEY_CURRENT_USER, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", 0),
    ]

    seen = set()
    for root, path, flags in reg_paths:
        try:
            key = winreg.OpenKey(root, path, 0, winreg.KEY_READ | flags)
        except Exception:
            continue

        num_subkeys = winreg.QueryInfoKey(key)[0]
        for i in range(num_subkeys):
            try:
                subkey_name = winreg.EnumKey(key, i)
                subkey = winreg.OpenKey(key, subkey_name)
                
                try:
                    is_system = winreg.QueryValueEx(subkey, "SystemComponent")[0]
                    if is_system == 1:
                        winreg.CloseKey(subkey)
                        continue
                except Exception:
                    pass

                try:
                    display_name = winreg.QueryValueEx(subkey, "DisplayName")[0].strip()
                except Exception:
                    winreg.CloseKey(subkey)
                    continue

                if not display_name:
                    winreg.CloseKey(subkey)
                    continue

                try:
                    display_version = str(winreg.QueryValueEx(subkey, "DisplayVersion")[0]).strip()
                except Exception:
                    display_version = ""

                try:
                    publisher = str(winreg.QueryValueEx(subkey, "Publisher")[0]).strip()
                except Exception:
                    publisher = ""

                try:
                    install_date = str(winreg.QueryValueEx(subkey, "InstallDate")[0]).strip()
                except Exception:
                    install_date = ""

                winreg.CloseKey(subkey)

                key_id = (display_name.lower(), display_version.lower())
                if key_id not in seen:
                    seen.add(key_id)
                    apps.append({
                        "name": display_name,
                        "version": display_version,
                        "publisher": publisher,
                        "install_date": install_date
                    })
            except Exception:
                pass
        winreg.CloseKey(key)

    apps.sort(key=lambda x: x["name"].lower())
    return apps

def get_network_and_location():
    local_ip = socket.gethostbyname(socket.gethostname())
    ssid = "LAN/Unknown WiFi"
    
    try:
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        out = subprocess.check_output(['netsh', 'wlan', 'show', 'interfaces'], stderr=subprocess.DEVNULL, startupinfo=startupinfo).decode('utf-8', errors='ignore')
        for line in out.split('\n'):
            if "SSID" in line and "BSSID" not in line:
                ssid = line.split(':')[1].strip()
                break
    except Exception:
        pass
        
    coords = "Unknown"
    ps_geo = (
        "Add-Type -AssemblyName System.Device; "
        "$g = New-Object System.Device.Location.GeoCoordinateWatcher; "
        "$g.Start(); "
        "$timeout = 8; "
        "while (($g.Status -ne [System.Device.Location.GeoPositionStatus]::Ready) -and ($timeout -gt 0)) { Start-Sleep -Seconds 1; $timeout-- }; "
        "$loc = $g.Position.Location; "
        "if (-not $loc.IsUnknown) { Write-Host \"$($loc.Latitude),$($loc.Longitude)\" } else { Write-Host \"Unknown\" }; "
        "$g.Stop()"
    )
    try:
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        out_geo = subprocess.check_output(['powershell', '-NoProfile', '-WindowStyle', 'Hidden', '-Command', ps_geo], startupinfo=startupinfo, timeout=12).decode('utf-8').strip()
        if out_geo and out_geo != "Unknown":
            coords = out_geo
    except Exception:
        pass

    return {"local_ip": local_ip, "ssid": ssid, "coordinates": coords}

def clean_window_title(title):
    if not title or not title.strip():
        return "Desktop/Idle"
    
    cleaned = re.sub(r'^\(\d+\)\s*', '', title).strip()

    if re.search(r'\b(microsoft\s+edge|msedge)\b', cleaned, re.IGNORECASE) or cleaned.lower().endswith("microsoft edge"):
        return "Microsoft Edge"
    if re.search(r'\b(google\s+chrome|chrome)\b', cleaned, re.IGNORECASE) or cleaned.lower().endswith("google chrome"):
        return "Google Chrome"
    if re.search(r'\b(mozilla\s+firefox|firefox)\b', cleaned, re.IGNORECASE) or cleaned.lower().endswith("firefox"):
        return "Mozilla Firefox"
    if re.search(r'\b(brave)\b', cleaned, re.IGNORECASE):
        return "Brave Browser"
    if re.search(r'\b(opera)\b', cleaned, re.IGNORECASE):
        return "Opera Browser"

    if re.search(r'\b(microsoft\s+teams|teams)\b', cleaned, re.IGNORECASE):
        return "Microsoft Teams"

    if re.search(r'\b(excel)\b', cleaned, re.IGNORECASE) or cleaned.lower().endswith("- excel") or ".xlsx" in cleaned.lower() or ".xls" in cleaned.lower():
        return "Microsoft Excel"
    if re.search(r'\b(powerpoint)\b', cleaned, re.IGNORECASE) or cleaned.lower().endswith("- powerpoint") or ".pptx" in cleaned.lower() or ".ppt" in cleaned.lower():
        return "Microsoft PowerPoint"
    if re.search(r'\b(word)\b', cleaned, re.IGNORECASE) or cleaned.lower().endswith("- word") or ".docx" in cleaned.lower() or ".doc" in cleaned.lower():
        return "Microsoft Word"
    if re.search(r'\b(outlook)\b', cleaned, re.IGNORECASE) or cleaned.lower().endswith("- outlook"):
        return "Microsoft Outlook"
    if re.search(r'\b(onenote)\b', cleaned, re.IGNORECASE):
        return "Microsoft OneNote"
    if re.search(r'\b(onedrive|microsoft\s+onedrive)\b', cleaned, re.IGNORECASE):
        return "Microsoft OneDrive"

    if re.search(r'\b(file\s+explorer|explorer)\b', cleaned, re.IGNORECASE) or cleaned.lower().endswith("- file explorer") or "downloads" in cleaned.lower() or "documents" in cleaned.lower():
        return "File Explorer"
    if re.search(r'\b(notepad)\b', cleaned, re.IGNORECASE):
        return "Notepad"
    if re.search(r'\b(remote\s+desktop\s+connection|mstsc)\b', cleaned, re.IGNORECASE):
        return "Remote Desktop Connection"
    if re.search(r'\b(lock\s+screen|windows\s+default\s+lock\s+screen)\b', cleaned, re.IGNORECASE):
        return "Windows Default Lock Screen"
    if re.search(r'\b(settings|pengaturan)\b', cleaned, re.IGNORECASE):
        return "Settings"
    if re.search(r'\b(task\s+manager)\b', cleaned, re.IGNORECASE):
        return "Task Manager"
    if re.search(r'\b(obs|obs\s+studio)\b', cleaned, re.IGNORECASE):
        return "OBS Studio"
    if re.search(r'\b(zenmap)\b', cleaned, re.IGNORECASE):
        return "Zenmap"
    if re.search(r'\b(whatsapp)\b', cleaned, re.IGNORECASE):
        return "WhatsApp"
    if re.search(r'\b(visual\s+studio\s+code|vscode)\b', cleaned, re.IGNORECASE):
        return "Visual Studio Code"
    if re.search(r'\b(command\s+prompt|powershell|cmd)\b', cleaned, re.IGNORECASE):
        return "Terminal / Command Prompt"

    if ' - ' in cleaned:
        parts = cleaned.split(' - ')
        last_part = parts[-1].strip()
        if 2 < len(last_part) < 50:
            return last_part

    return cleaned

def get_active_window():
    try:
        user32 = ctypes.windll.user32
        hwnd = user32.GetForegroundWindow()
        length = user32.GetWindowTextLengthW(hwnd)
        buff = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buff, length + 1)
        return clean_window_title(buff.value)
    except Exception:
        return "Unknown"

def is_valid_serial(sn):
    if not sn:
        return False
    sn_clean = sn.strip().lower()
    invalid_list = ["unknown", "to be filled by o.e.m.", "default string", "n/a", "none", "0", "123456789", "system serial number", "chassis serial number"]
    if sn_clean in invalid_list or sn_clean.startswith("sn-"):
        return False
    return len(sn.strip()) >= 3

def get_serial_number():
    """Membaca Serial Number BIOS asli via Registry, WMIC & CIM Fallback dengan Caching."""
    global _CACHED_SERIAL_NUMBER
    if _CACHED_SERIAL_NUMBER and is_valid_serial(_CACHED_SERIAL_NUMBER):
        return _CACHED_SERIAL_NUMBER

    # 1. Coba baca via Registry Native (Sangat Cepat & Ringan)
    try:
        key = winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"HARDWARE\DESCRIPTION\System\BIOS")
        for val_name in ["SystemSerialNumber", "BaseBoardSerialNumber", "SerialNumber"]:
            try:
                sn = winreg.QueryValueEx(key, val_name)[0].strip()
                if is_valid_serial(sn):
                    winreg.CloseKey(key)
                    _CACHED_SERIAL_NUMBER = sn
                    return _CACHED_SERIAL_NUMBER
            except FileNotFoundError:
                continue
        winreg.CloseKey(key)
    except Exception:
        pass

    # 2. Coba WMIC Native (Cepat jika tersedia)
    try:
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        wmic_out = subprocess.check_output(
            ['wmic', 'bios', 'get', 'serialnumber'],
            startupinfo=startupinfo, timeout=6
        ).decode('utf-8', errors='ignore')
        for line in wmic_out.splitlines():
            line = line.strip()
            if line and "serialnumber" not in line.lower() and is_valid_serial(line):
                _CACHED_SERIAL_NUMBER = line
                return _CACHED_SERIAL_NUMBER
    except Exception:
        pass

    # 3. Coba PowerShell WMI / CIM (Win32_BIOS, Win32_ComputerSystemProduct, Win32_BaseBoard)
    try:
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        ps_cmd = (
            "$s = (Get-CimInstance Win32_BIOS -ErrorAction SilentlyContinue).SerialNumber; "
            "if (-not $s -or $s -match '^(none|n/a|default string|unknown|0)$') { "
            "  $s = (Get-CimInstance Win32_ComputerSystemProduct -ErrorAction SilentlyContinue).IdentifyingNumber "
            "}; "
            "if (-not $s -or $s -match '^(none|n/a|default string|unknown|0)$') { "
            "  $s = (Get-CimInstance Win32_BaseBoard -ErrorAction SilentlyContinue).SerialNumber "
            "}; "
            "if ($s) { $s.Trim() }"
        )
        wmi_sn = subprocess.check_output(
            ['powershell', '-NoProfile', '-WindowStyle', 'Hidden', '-Command', ps_cmd],
            startupinfo=startupinfo, timeout=12
        ).decode('utf-8', errors='ignore').strip()

        if is_valid_serial(wmi_sn):
            _CACHED_SERIAL_NUMBER = wmi_sn
            return _CACHED_SERIAL_NUMBER
    except Exception:
        pass

    # JANGAN simpan fallback SN- ke dalam cache agar pemanggilan berikutnya tetap mencoba query SN asli
    return f"SN-{socket.gethostname()}"

# --- [ SESSION TRACKING ] ---

def record_session_start(username, display_name):
    session_id = str(uuid.uuid4())
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute("INSERT INTO session_log (session_id, username, display_name, login_time, last_seen, is_sent) VALUES (?, ?, ?, ?, ?, 0)",
              (session_id, username, display_name, now, now))
    conn.commit()
    conn.close()
    return session_id

def update_session_heartbeat(session_id):
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute("UPDATE session_log SET last_seen = ? WHERE session_id = ?", (now, session_id))
    conn.commit()
    conn.close()

def get_completed_sessions():
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute("SELECT session_id, username, display_name, login_time, last_seen FROM session_log WHERE is_sent = 0")
    rows = c.fetchall()
    conn.close()
    
    sessions = []
    for row in rows:
        login_dt = datetime.strptime(row[3], "%Y-%m-%d %H:%M:%S")
        last_dt = datetime.strptime(row[4], "%Y-%m-%d %H:%M:%S")
        duration_min = round((last_dt - login_dt).total_seconds() / 60.0, 1)
        sessions.append({
            "session_id": row[0],
            "username": row[1],
            "display_name": row[2],
            "login_time": row[3],
            "last_seen": row[4],
            "duration_minutes": duration_min
        })
    return sessions

def mark_sessions_sent(session_ids):
    if not session_ids:
        return
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    for sid in session_ids:
        c.execute("UPDATE session_log SET is_sent = 1 WHERE session_id = ?", (sid,))
    conn.commit()
    conn.close()

# --- [ APP TIMELINE GENERATOR ] ---

def generate_app_timeline(rows, record_interval=300):
    """
    Mengelompokkan rangkaian log snapshot aktivitas menjadi interval timeline
    dengan Start Time, End Time, durasi menit, nama aplikasi, dan status AFK/Idle.
    """
    if not rows:
        return []

    timeline = []
    current_app = None
    current_is_afk = False
    start_time_str = None
    last_time_str = None
    block_count = 0

    for r in rows:
        t_stamp, app = r[0], r[4]
        is_afk = True if ("[AFK" in app or "Idle" in app) else False
        time_part = t_stamp[11:19]  # HH:MM:SS

        if current_app is None:
            current_app = app
            current_is_afk = is_afk
            start_time_str = time_part
            last_time_str = time_part
            block_count = 1
        elif current_app != app:
            dur_min = round(block_count * (record_interval / 60.0), 1)
            timeline.append({
                "app_name": current_app,
                "start_time": start_time_str,
                "end_time": last_time_str,
                "duration_minutes": dur_min,
                "is_afk": current_is_afk
            })
            current_app = app
            current_is_afk = is_afk
            start_time_str = time_part
            last_time_str = time_part
            block_count = 1
        else:
            last_time_str = time_part
            block_count += 1

    if current_app is not None:
        dur_min = round(block_count * (record_interval / 60.0), 1)
        timeline.append({
            "app_name": current_app,
            "start_time": start_time_str,
            "end_time": last_time_str,
            "duration_minutes": dur_min,
            "is_afk": current_is_afk
        })

    return timeline

# --- [ SECURE TRANSPORT: HTTPS REST CLIENT ] ---

def send_payload_https(payload):
    """
    Mengirim payload telemetri ke server hub melalui HTTPS REST API (TLS 1.2/1.3 Encrypted).
    Hanya mengembalikan True jika server merespons dengan HTTP Status 200 OK dan body success.
    """
    # 1. Coba HTTPS REST API (Primary - Encrypted)
    try:
        resp = requests.post(
            SERVER_HTTPS_URL,
            json=payload,
            headers={"Content-Type": "application/json", "User-Agent": "ITAssetTrack-Pulse-Agent/1.2.0"},
            timeout=15,
            verify=False
        )
        if resp.status_code == 200:
            res_json = resp.json()
            if res_json.get("status") == "success":
                return True
    except Exception:
        pass

    # 2. Fallback: Coba Raw TCP Socket jika port HTTPS belum aktif di server tujuan
    try:
        payload_str = json.dumps(payload)
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.settimeout(12)
            s.connect((SERVER_IP, RAW_PORT))
            s.sendall(payload_str.encode('utf-8'))
        return True
    except Exception:
        pass

    return False

# --- [ ORPHAN DATA RECOVERY ] ---

def flush_orphan_data(current_user):
    """Kirim data sisa dari user sebelumnya (jika user berganti) sebelum mulai collect baru."""
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute("SELECT DISTINCT username FROM activity_log WHERE username != ? AND username IS NOT NULL AND is_synced = 0", (current_user,))
    orphan_users = c.fetchall()
    
    for (orphan_user,) in orphan_users:
        c.execute("SELECT * FROM activity_log WHERE username = ? AND is_synced = 0 ORDER BY timestamp ASC", (orphan_user,))
        rows = c.fetchall()
        if not rows:
            continue
        
        start_time = rows[0][0]
        end_time = rows[-1][0]
        
        app_counts = {}
        battery_timeline = []
        cpu_list, ram_list, gpu_list = [], [], []
        current_state, b_start_time, b_start_pct, last_time, last_pct = None, None, None, None, None
        last_bat_health = None

        for row in rows:
            t_stamp, _user, b_pct, is_plugged, app = row[0], row[1], row[2], bool(row[3]), row[4]
            cpu_list.append(row[5])
            ram_list.append(row[6])
            gpu_list.append(row[7])
            last_bat_health = row[8]
            
            app_counts[app] = app_counts.get(app, 0) + 1
            state_str = "Charging" if is_plugged else "Discharging"
            time_only = t_stamp[11:19]
            
            if current_state is None:
                current_state = state_str
                b_start_time = time_only
                b_start_pct = b_pct
            elif current_state != state_str:
                battery_timeline.append({
                    "status": current_state,
                    "start_time": b_start_time,
                    "end_time": last_time,
                    "start_pct": b_start_pct,
                    "end_pct": last_pct
                })
                current_state = state_str
                b_start_time = time_only
                b_start_pct = b_pct
            last_time = time_only
            last_pct = b_pct

        if current_state is not None:
            battery_timeline.append({
                "status": current_state,
                "start_time": b_start_time,
                "end_time": last_time,
                "start_pct": b_start_pct,
                "end_pct": last_pct
            })

        c.execute("SELECT display_name FROM session_log WHERE username = ? ORDER BY login_time DESC LIMIT 1", (orphan_user,))
        dn_row = c.fetchone()
        orphan_display_name = dn_row[0] if dn_row and dn_row[0] else ""

        orphan_sessions = []
        c.execute("SELECT session_id, username, display_name, login_time, last_seen FROM session_log WHERE username = ? AND is_sent = 0", (orphan_user,))
        for sr in c.fetchall():
            login_dt = datetime.strptime(sr[3], "%Y-%m-%d %H:%M:%S")
            last_dt = datetime.strptime(sr[4], "%Y-%m-%d %H:%M:%S")
            duration_min = round((last_dt - login_dt).total_seconds() / 60.0, 1)
            orphan_sessions.append({
                "session_id": sr[0], "username": sr[1], "display_name": sr[2],
                "login_time": sr[3], "last_seen": sr[4], "duration_minutes": duration_min
            })

        app_timeline = generate_app_timeline(rows, RECORD_INTERVAL)

        payload = {
            "report_type": "Orphan Recovery",
            "schedule_triggered": None,
            "record_interval_seconds": RECORD_INTERVAL,
            "hostname": socket.gethostname(),
            "logged_in_user": orphan_user,
            "display_name": orphan_display_name,
            "serial_number": get_serial_number(),
            "os_version": get_os_version(),
            "network": get_network_and_location(),
            "battery_health_pct": last_bat_health,
            "period_start": start_time,
            "period_end": end_time,
            "cpu_uptime_seconds": get_cpu_uptime_seconds(),
            "hardware_stats": {
                "cpu": {"max": max(cpu_list), "min": min(cpu_list), "avg": round(sum(cpu_list)/len(cpu_list))},
                "ram": {"max": max(ram_list), "min": min(ram_list), "avg": round(sum(ram_list)/len(ram_list))},
                "gpu": {"max": max(gpu_list), "min": min(gpu_list), "avg": round(sum(gpu_list)/len(gpu_list))}
            },
            "battery_timeline": battery_timeline,
            "app_timeline": app_timeline,
            "top_apps": app_counts,
            "login_sessions": orphan_sessions
        }

        if send_payload_https(payload):
            c.execute("DELETE FROM activity_log WHERE username = ?", (orphan_user,))
            for sess in orphan_sessions:
                c.execute("UPDATE session_log SET is_sent = 1 WHERE session_id = ?", (sess["session_id"],))
            conn.commit()
    
    conn.close()

# --- [ BUILD & SEND SUMMARY ] ---

def build_and_send_summary(schedule_time, current_user, display_name, report_type="Scheduled"):
    global _LAST_SYNC_TIME_STR
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    
    # Hanya ambil data milik user saat ini yang belum terkirim (is_synced = 0)
    c.execute("SELECT * FROM activity_log WHERE username = ? AND is_synced = 0 ORDER BY timestamp ASC", (current_user,))
    rows = c.fetchall()
    
    if not rows:
        conn.close()
        return False

    start_time = rows[0][0]
    end_time = rows[-1][0]
    
    # Jika start_time == end_time (hanya ada 1 row snapshot), ambil login_time dari session_log agar durasi riil
    if start_time == end_time or len(rows) == 1:
        c.execute("SELECT login_time FROM session_log WHERE username = ? ORDER BY login_time DESC LIMIT 1", (current_user,))
        s_row = c.fetchone()
        if s_row and s_row[0]:
            start_time = s_row[0]
    
    app_counts = {}
    battery_timeline = []
    cpu_list, ram_list, gpu_list = [], [], []
    
    current_state, b_start_time, b_start_pct, last_time, last_pct = None, None, None, None, None
    last_bat_health = None

    for row in rows:
        t_stamp, _user, b_pct, is_plugged, app = row[0], row[1], row[2], bool(row[3]), row[4]
        
        cpu_list.append(row[5])
        ram_list.append(row[6])
        gpu_list.append(row[7])
        last_bat_health = row[8]
        
        app_counts[app] = app_counts.get(app, 0) + 1
        
        state_str = "Charging" if is_plugged else "Discharging"
        time_only = t_stamp[11:19] 
        
        if current_state is None:
            current_state = state_str
            b_start_time = time_only
            b_start_pct = b_pct
        elif current_state != state_str:
            battery_timeline.append({
                "status": current_state,
                "start_time": b_start_time,
                "end_time": last_time,
                "start_pct": b_start_pct,
                "end_pct": last_pct
            })
            current_state = state_str
            b_start_time = time_only
            b_start_pct = b_pct
            
        last_time = time_only
        last_pct = b_pct

    if current_state is not None:
        battery_timeline.append({
            "status": current_state,
            "start_time": b_start_time,
            "end_time": last_time,
            "start_pct": b_start_pct,
            "end_pct": last_pct
        })

    login_sessions = get_completed_sessions()
    app_timeline = generate_app_timeline(rows, RECORD_INTERVAL)

    # Kirim network usage & daftar software terinstall hanya pada jadwal terakhir (16:24 / SCHEDULE_TIMES[-1]) atau saat Manual Sync
    is_last_schedule = (schedule_time == SCHEDULE_TIMES[-1]) or (report_type == "Manual Sync")
    daily_net = get_daily_network_usage() if is_last_schedule else None
    installed_sw = get_installed_software() if is_last_schedule else None

    payload = {
        "report_type": report_type,
        "schedule_triggered": schedule_time,
        "record_interval_seconds": RECORD_INTERVAL, 
        "hostname": socket.gethostname(),
        "logged_in_user": current_user,
        "display_name": display_name,
        "serial_number": get_serial_number(),
        "os_version": get_os_version(),
        "network": get_network_and_location(),
        "daily_network_usage": daily_net,
        "installed_software": installed_sw,
        "battery_health_pct": last_bat_health, 
        "period_start": start_time,
        "period_end": end_time,
        "cpu_uptime_seconds": get_cpu_uptime_seconds(), 
        "hardware_stats": {
            "cpu": {"max": max(cpu_list), "min": min(cpu_list), "avg": round(sum(cpu_list)/len(cpu_list))},
            "ram": {"max": max(ram_list), "min": min(ram_list), "avg": round(sum(ram_list)/len(ram_list))},
            "gpu": {"max": max(gpu_list), "min": min(gpu_list), "avg": round(sum(gpu_list)/len(gpu_list))}
        },
        "battery_timeline": battery_timeline,
        "app_timeline": app_timeline,
        "top_apps": app_counts,
        "login_sessions": login_sessions
    }

    # Transaksi Pengiriman: Hanya bersihkan database lokal JIKA server merespons HTTP 200 OK
    is_success = send_payload_https(payload)
    if is_success:
        now_sync_str = datetime.now().strftime("%d/%m %H:%M")
        _LAST_SYNC_TIME_STR = now_sync_str
        
        # Hapus data yang berhasil terkirim
        c.execute("DELETE FROM activity_log WHERE username = ? AND timestamp <= ?", (current_user, end_time))
        conn.commit()
        conn.close()
        
        sent_ids = [s["session_id"] for s in login_sessions]
        mark_sessions_sent(sent_ids)
        return True
    else:
        conn.close()
        return False

# --- [ WORKER TELEMETRY LOOP ] ---

def telemetry_worker():
    setup_database()
    
    current_user = getpass.getuser()
    display_name = get_user_display_name()
    
    session_id = record_session_start(current_user, display_name)
    flush_orphan_data(current_user)
    
    last_record_time = 0 
    cached_bat_health = get_battery_health_powercfg()
    last_bat_health_check = time.time()
    sent_today = set()

    while True:
        try:
            current_time = time.time()
            now_time_obj = datetime.now()
            current_time_str = now_time_obj.strftime("%H:%M")
            
            if current_time - last_bat_health_check >= BATTERY_HEALTH_INTERVAL:
                cached_bat_health = get_battery_health_powercfg()
                last_bat_health_check = current_time
            
            if current_time - last_record_time >= RECORD_INTERVAL:
                bat = psutil.sensors_battery()
                bat_pct = bat.percent if bat else 100
                is_plugged = bat.power_plugged if bat else True
                
                cpu_val = int(psutil.cpu_percent(interval=1))
                ram_val = int(psutil.virtual_memory().percent)
                gpu_val = get_gpu_usage()
                
                idle_seconds = get_idle_time()
                if idle_seconds >= AFK_THRESHOLD:
                    active_win = "[AFK / Tidak Ada Aktivitas]"
                else:
                    active_win = get_active_window()
                
                save_to_local(current_user, bat_pct, is_plugged, active_win, cpu_val, ram_val, gpu_val, cached_bat_health)
                update_session_heartbeat(session_id)
                last_record_time = current_time

            if current_time_str in SCHEDULE_TIMES and current_time_str not in sent_today:
                if build_and_send_summary(current_time_str, current_user, display_name, report_type="Scheduled"):
                    sent_today.add(current_time_str)
            
            if current_time_str == "00:00":
                sent_today.clear()

        except Exception:
            pass

        time.sleep(10)

# --- [ SYSTEM TRAY GUI (WINDOWS NOTIFICATION AREA) ] ---

def get_tray_icon_image():
    """Mencari dan memuat file itassettrack.ico untuk icon tray secara dinamis."""
    from PIL import Image, ImageDraw
    
    # Coba dari internal bundle PyInstaller atau direktori aplikasi
    possible_paths = [
        os.path.join(getattr(sys, '_MEIPASS', ''), 'itassettrack.ico'),
        os.path.join(os.path.dirname(os.path.abspath(sys.argv[0])), 'itassettrack.ico'),
        os.path.join(os.path.dirname(os.path.abspath(__file__)), 'itassettrack.ico'),
        r'C:\Program Files\ITAssetTrack\itassettrack.ico'
    ]
    for p in possible_paths:
        if os.path.exists(p):
            try:
                return Image.open(p)
            except Exception:
                pass

    # Fallback: Buat icon lingkaran hijau elegan secara programmatic jika file .ico belum tersedia
    img = Image.new('RGBA', (64, 64), color=(0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.ellipse((4, 4, 60, 60), fill=(24, 160, 88), outline=(255, 255, 255), width=3)
    return img

def on_manual_sync(icon, item):
    """Trigger sinkronisasi manual saat user memilih Sync Now dari Tray Menu."""
    def _do_sync():
        user = getpass.getuser()
        dn = get_user_display_name()
        # 1. Simpan snapshot detik ini terlebih dahulu
        record_current_snapshot()
        # 2. Kirim seluruh akumulasi data yang tersimpan sebagai Manual Sync
        build_and_send_summary(schedule_time=None, current_user=user, display_name=dn, report_type="Manual Sync")
    threading.Thread(target=_do_sync, daemon=True).start()

def get_icon_path():
    """Mencari lokasi fisik file itassettrack.ico."""
    possible_paths = [
        os.path.join(getattr(sys, '_MEIPASS', ''), 'itassettrack.ico'),
        os.path.join(os.path.dirname(os.path.abspath(sys.argv[0])), 'itassettrack.ico'),
        os.path.join(os.path.dirname(os.path.abspath(__file__)), 'itassettrack.ico'),
        r'C:\Program Files\ITAssetTrack\itassettrack.ico'
    ]
    for p in possible_paths:
        if p and os.path.exists(p):
            return p
    return None

def on_show_about(icon, item):
    """Menampilkan dialog informasi korporat CIS INFRA_DEPT dengan logo resmi ITAssetTrack."""
    def _show():
        try:
            import tkinter as tk
            from PIL import Image, ImageTk

            root = tk.Tk()
            root.title("About ITAssetTrack")
            root.resizable(False, False)
            root.attributes("-topmost", True)
            root.configure(bg="#f8fafc")

            w, h = 430, 220
            ws = root.winfo_screenwidth()
            hs = root.winfo_screenheight()
            x = max(0, (ws // 2) - (w // 2))
            y = max(0, (hs // 2) - (h // 2))
            root.geometry(f"{w}x{h}+{x}+{y}")

            ico_path = get_icon_path()
            if ico_path:
                try:
                    root.iconbitmap(ico_path)
                except Exception:
                    pass

            # Main content frame
            frame = tk.Frame(root, bg="#f8fafc", padx=20, pady=18)
            frame.pack(fill="both", expand=True)

            # Left Icon (ITAssetTrack Logo)
            if ico_path:
                try:
                    pil_img = Image.open(ico_path).resize((64, 64), Image.Resampling.LANCZOS)
                    tk_img = ImageTk.PhotoImage(pil_img)
                    img_lbl = tk.Label(frame, image=tk_img, bg="#f8fafc")
                    img_lbl.image = tk_img
                    img_lbl.grid(row=0, column=0, rowspan=4, padx=(0, 18), sticky="n")
                except Exception:
                    pass

            # Right Texts
            title_lbl = tk.Label(frame, text="ITAssetTrack v1.2.0", font=("Segoe UI", 13, "bold"), fg="#0f172a", bg="#f8fafc")
            title_lbl.grid(row=0, column=1, sticky="w")

            dev_lbl = tk.Label(
                frame,
                text="Developed by:\nCIS Division | INFRA_DEPT Department\nInfraHub & ITAssetTrack Enterprise \u00a9 2026",
                font=("Segoe UI", 9),
                fg="#334155",
                bg="#f8fafc",
                justify="left"
            )
            dev_lbl.grid(row=2, column=1, sticky="w")

            # Bottom Button Bar
            btn_frame = tk.Frame(root, bg="#f1f5f9", padx=15, pady=10)
            btn_frame.pack(fill="x", side="bottom")

            ok_btn = tk.Button(
                btn_frame,
                text="OK",
                font=("Segoe UI", 9, "bold"),
                width=10,
                bg="#0284c7",
                fg="white",
                activebackground="#0369a1",
                activeforeground="white",
                relief="flat",
                cursor="hand2",
                command=root.destroy
            )
            ok_btn.pack(side="right")

            root.bind("<Return>", lambda e: root.destroy())
            root.bind("<Escape>", lambda e: root.destroy())
            ok_btn.focus_set()

            root.mainloop()

        except Exception:
            # Fallback jika tkinter mengalami kendala
            ctypes.windll.user32.MessageBoxW(
                0,
                "ITAssetTrack v1.2.0\n\n"
                "Developed by:\n"
                "CIS Division | INFRA_DEPT Department\n"
                "InfraHub & ITAssetTrack Enterprise \u00a9 2026",
                "About",
                0x00000040 | 0x00010000 | 0x00002000
            )

    threading.Thread(target=_show, daemon=True).start()

def setup_tray():
    global _TRAY_ICON
    import pystray
    
    img = get_tray_icon_image()
    
    def get_last_sync_text(item):
        return f"Last Sync: {_LAST_SYNC_TIME_STR}"
    
    menu = pystray.Menu(
        pystray.MenuItem("ITAssetTrack", None, enabled=False),
        pystray.MenuItem(get_last_sync_text, None, enabled=False),
        pystray.Menu.SEPARATOR,
        pystray.MenuItem("Sync Now", on_manual_sync),
        pystray.MenuItem("About", on_show_about)
    )
    
    _TRAY_ICON = pystray.Icon(
        "ITAssetTrack_Pulse",
        img,
        "ITAssetTrack",
        menu
    )
    _TRAY_ICON.run()

def run_agent():
    # 1. Jalankan Telemetry Worker di Background Daemon Thread
    worker_thread = threading.Thread(target=telemetry_worker, daemon=True)
    worker_thread.start()
    
    # 2. Jalankan System Tray Icon di Main Thread
    try:
        setup_tray()
    except Exception:
        while True:
            time.sleep(60)

if __name__ == "__main__":
    run_agent()