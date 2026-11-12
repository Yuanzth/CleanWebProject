import os
import sys
import time
import random
import uuid
import datetime
try:
    import zoneinfo
    JAKARTA_TZ = zoneinfo.ZoneInfo(os.getenv('TZ', 'Asia/Jakarta'))
    def get_now():
        return datetime.datetime.now(JAKARTA_TZ).replace(tzinfo=None)
except Exception:
    def get_now():
        return datetime.datetime.now()
import pymssql

# Force line-buffered stdout so logs show immediately in Docker
try:
    sys.stdout.reconfigure(line_buffering=True)
except Exception:
    pass

DB_SERVER = os.getenv("DB_SERVER", "localhost")
DB_PORT = int(os.getenv("DB_PORT", "1433"))
DB_USER = os.getenv("DB_USER", "sa")
DB_PASSWORD = os.getenv("DB_PASSWORD", "Admin123#")
INTERVAL_SECONDS = int(os.getenv("INTERVAL_SECONDS", "30"))

def wait_for_database(db_name, max_retries=60, delay=3):
    """Menunggu hingga database target siap menerima koneksi."""
    print(f"[*] Menghubungi SQL Server {DB_SERVER}:{DB_PORT} untuk database [{db_name}]...")
    for i in range(max_retries):
        try:
            conn = pymssql.connect(
                server=DB_SERVER,
                port=DB_PORT,
                user=DB_USER,
                password=DB_PASSWORD,
                database=db_name,
                login_timeout=5
            )
            conn.close()
            print(f"[+] Berhasil terhubung ke database [{db_name}]!")
            return True
        except Exception as e:
            if i % 5 == 0:
                print(f"[-] Menunggu database [{db_name}] siap ({i+1}/{max_retries})... ({e})")
            time.sleep(delay)
    print(f"[!] Timeout: Gagal terhubung ke [{db_name}] setelah {max_retries * delay} detik.")
    return False

def get_connection(db_name):
    return pymssql.connect(
        server=DB_SERVER,
        port=DB_PORT,
        user=DB_USER,
        password=DB_PASSWORD,
        database=db_name,
        autocommit=True
    )

def simulate_ups_monitoring(conn_infra_dept):
    """Menghasilkan data log telemetri UPS dan sensor untuk db_infrahub."""
    try:
        cursor = conn_infra_dept.cursor(as_dict=True)
        # Ambil daftar perangkat bertipe UPS
        cursor.execute("""
            SELECT d.id, d.device_name, d.site_id 
            FROM tbl_m_devices d
            JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
            WHERE a.type_name = 'UPS' AND d.is_active = 1
        """)
        devices = cursor.fetchall()

        if not devices:
            # Fallback ambil sembarang device jika asset_type belum terisi
            cursor.execute("SELECT TOP 5 id, device_name, site_id FROM tbl_m_devices WHERE is_active = 1")
            devices = cursor.fetchall()

        now = get_now()
        inserted_count = 0

        for dev in devices:
            dev_id = str(dev['id'])
            log_id = str(uuid.uuid4())
            vin_a = round(219.0 + random.uniform(-3.5, 3.5), 2)
            vin_b = round(220.0 + random.uniform(-3.0, 3.0), 2)
            vin_c = round(221.0 + random.uniform(-3.0, 3.0), 2)
            vout_a = round(220.0 + random.uniform(-0.8, 0.8), 2)
            vout_b = round(220.0 + random.uniform(-0.8, 0.8), 2)
            vout_c = round(220.0 + random.uniform(-0.8, 0.8), 2)
            battery_pct = round(random.uniform(96.0, 100.0), 1)
            battery_v = round(430.0 + random.uniform(0.0, 15.0), 1)
            load_pct = round(random.uniform(35.0, 68.0), 1)
            total_watts = round(load_pct * 48.0 + random.uniform(-30, 30), 0)
            iin_a = round((total_watts / (vin_a * 3.0)) + random.uniform(0.2, 0.6), 1)
            iin_b = round((total_watts / (vin_b * 3.0)) + random.uniform(0.2, 0.6), 1)
            iin_c = round((total_watts / (vin_c * 3.0)) + random.uniform(0.2, 0.6), 1)
            iout_a = round((total_watts / (vout_a * 3.0)) + random.uniform(0.1, 0.5), 1)
            iout_b = round((total_watts / (vout_b * 3.0)) + random.uniform(0.1, 0.5), 1)
            iout_c = round((total_watts / (vout_c * 3.0)) + random.uniform(0.1, 0.5), 1)
            temp = round(26.0 + random.uniform(0.5, 3.5), 1)       # UPS Machine Temp
            room_temp = round(22.0 + random.uniform(0.2, 2.8), 1)  # Room Ambient Temp
            grounding = round(random.uniform(0.8, 2.2), 2)         # Grounding Resistance (0.8 - 2.2 Ohm)
            freq = round(50.0 + random.uniform(-0.1, 0.1), 2)
            runtime_min = int(round(90.0 * (battery_pct / 100.0) * (50.0 / load_pct)))

            cursor.execute("""
                INSERT INTO tbl_t_ups_logs (
                    id, device_id, device_time, voltage_in_a, voltage_in_b, voltage_in_c,
                    current_in_a, current_in_b, current_in_c,
                    input_frequency, voltage_out_a, voltage_out_b, voltage_out_c,
                    current_out_a, current_out_b, current_out_c,
                    output_frequency, battery_percent, battery_voltage, battery_runtime_min,
                    load_percent, total_watts, ambient_temp, room_temp,
                    grounding_resistance, ups_status, created_at
                ) VALUES (
                    %s, %s, %s, %s, %s, %s,
                    %s, %s, %s,
                    %s, %s, %s, %s,
                    %s, %s, %s,
                    %s, %s, %s, %s,
                    %s, %s, %s, %s,
                    %s, %s, %s
                )
            """, (
                log_id, dev_id, now, vin_a, vin_b, vin_c,
                iin_a, iin_b, iin_c,
                freq, vout_a, vout_b, vout_c,
                iout_a, iout_b, iout_c,
                freq, battery_pct, battery_v, runtime_min,
                load_pct, total_watts, temp, room_temp,
                grounding, 'On line', now
            ))
            inserted_count += 1

        print(f"[{now.strftime('%H:%M:%S')}] [Monitoring Hub] Injeksi {inserted_count} log telemetri UPS baru.")

    except Exception as ex:
        print(f"[!] Error saat simulasi UPS Monitoring: {ex}")

def simulate_asset_pulse(conn_itassettrack):
    """Menghasilkan status heartbeat laptop/PC untuk db_itassettrack."""
    try:
        cursor = conn_itassettrack.cursor(as_dict=True)

        # 1. Update status online sejumlah aset secara dinamis
        cursor.execute("SELECT TOP 25 Id, SerialNumber, LastLoggedInUser FROM itassettrack.tbl_t_asset")
        assets = cursor.fetchall()

        if assets:
            now = get_now()
            online_count = 0
            for a in assets:
                is_online = 1 if random.random() > 0.15 else 0
                batt = random.randint(75, 100)
                ip = f"10.11.{random.randint(1, 15)}.{random.randint(2, 250)}"
                
                cursor.execute("""
                    UPDATE itassettrack.tbl_t_asset
                    SET IsOnline = %s,
                        LastBatteryHealthPct = %s,
                        LastIp = %s,
                        UpdatedAt = %s
                    WHERE Id = %s
                """, (is_online, batt, ip, now, a['Id']))
                if is_online:
                    online_count += 1

            print(f"[{now.strftime('%H:%M:%S')}] [Asset Track] Heartbeat {len(assets)} aset ({online_count} Online).")

    except Exception as ex:
        print(f"[!] Error saat simulasi Asset Pulse: {ex}")

def main():
    print("=" * 65)
    print("  InfraHub TELEMETRY & ASSET DUMMY DATA GENERATOR SERVICE")
    print("=" * 65)

    if not wait_for_database("db_infrahub") or not wait_for_database("db_itassettrack"):
        print("[!] Gagal terhubung ke database. Service generator berhenti.")
        sys.exit(1)

    print(f"[+] Service aktif! Menginjeksi data telemetri berkala setiap {INTERVAL_SECONDS} detik...")

    while True:
        try:
            with get_connection("db_infrahub") as conn_infra_dept:
                simulate_ups_monitoring(conn_infra_dept)

            with get_connection("db_itassettrack") as conn_itassettrack:
                simulate_asset_pulse(conn_itassettrack)

        except Exception as e:
            print(f"[!] Exception pada loop utama: {e}")

        time.sleep(INTERVAL_SECONDS)

if __name__ == "__main__":
    main()
