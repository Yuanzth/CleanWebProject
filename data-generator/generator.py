import os
import sys
import time
import math
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
DB_PASSWORD = os.getenv("DB_PASSWORD", "AdminPass123!")
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

def simulate_ups_monitoring(cursor, now):
    """Menghasilkan log telemetri UPS dengan skenario realistis (Normal, Battery Mode, dan Offline)."""
    try:
        cursor.execute("""
            SELECT d.id, d.device_name, d.ups_phase_type, d.ups_category, s.site_code
            FROM tbl_m_devices d
            JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
            LEFT JOIN tbl_m_sites s ON s.id = d.site_id
            WHERE a.type_name = 'UPS' AND d.is_active = 1
            ORDER BY d.device_name
        """)
        devices = cursor.fetchall()
        if not devices:
            return 0

        inserted_count = 0
        offline_count = 0
        battery_count = 0

        for idx, dev in enumerate(devices):
            dev_id = str(dev['id'])
            dev_name = dev['device_name'] or ''
            log_id = str(uuid.uuid4())

            # Simulasi Realistis:
            # 1. Dua unit disimulasikan OFFLINE (komunikasi putus / mati total):
            #    Misal: unit index 5 dan index 22
            if idx in (5, 22):
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
                        %s, %s, %s, 0.0, 0.0, 0.0,
                        0.0, 0.0, 0.0,
                        0.0, 0.0, 0.0, 0.0,
                        0.0, 0.0, 0.0,
                        0.0, 0.0, 0.0, 0,
                        0.0, 0.0, 24.0, 23.0,
                        NULL, 'Offline', %s
                    )
                """, (log_id, dev_id, now, now))
                inserted_count += 1
                offline_count += 1
                continue

            # 2. Satu unit (index 12) sama sekali TIDAK mengirim data (simulasi mati total / no logs):
            if idx == 12:
                # Dilewati agar data stale/kosong teruji di dashboard
                offline_count += 1
                continue

            # 3. Dua unit disimulasikan ON BATTERY (gangguan input PLN, beban ditenagai baterai):
            #    Misal: index 8 dan index 18
            if idx in (8, 18):
                batt_pct = round(random.uniform(45.0, 68.0), 1)
                batt_v = round(395.0 + random.uniform(0.0, 10.0), 1)
                load_pct = 42.0
                total_watts = 2100.0
                runtime_min = int(round(60.0 * (batt_pct / 100.0)))
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
                        %s, %s, %s, 0.0, 0.0, 0.0,
                        0.0, 0.0, 0.0,
                        0.0, 220.0, 220.0, 220.0,
                        3.2, 3.1, 3.2,
                        50.0, %s, %s, %s,
                        %s, %s, 31.5, 24.0,
                        1.4, 'On Battery', %s
                    )
                """, (log_id, dev_id, now, batt_pct, batt_v, runtime_min, load_pct, total_watts, now))
                inserted_count += 1
                battery_count += 1
                continue

            # 4. Sisa unit (~35 unit) berjalan NORMAL ONLINE
            vin_a = round(219.0 + random.uniform(-2.5, 2.5), 2)
            vin_b = round(220.0 + random.uniform(-2.5, 2.5), 2)
            vin_c = round(221.0 + random.uniform(-2.5, 2.5), 2)
            vout_a = round(220.0 + random.uniform(-0.8, 0.8), 2)
            vout_b = round(220.0 + random.uniform(-0.8, 0.8), 2)
            vout_c = round(220.0 + random.uniform(-0.8, 0.8), 2)
            battery_pct = round(random.uniform(96.0, 100.0), 1)
            battery_v = round(430.0 + random.uniform(0.0, 15.0), 1)
            load_pct = round(random.uniform(35.0, 68.0), 1)
            total_watts = round(load_pct * 48.0 + random.uniform(-20, 20), 0)
            iin_a = round((total_watts / (vin_a * 3.0)) + random.uniform(0.2, 0.5), 1)
            iin_b = round((total_watts / (vin_b * 3.0)) + random.uniform(0.2, 0.5), 1)
            iin_c = round((total_watts / (vin_c * 3.0)) + random.uniform(0.2, 0.5), 1)
            iout_a = round((total_watts / (vout_a * 3.0)) + random.uniform(0.1, 0.4), 1)
            iout_b = round((total_watts / (vout_b * 3.0)) + random.uniform(0.1, 0.4), 1)
            iout_c = round((total_watts / (vout_c * 3.0)) + random.uniform(0.1, 0.4), 1)
            temp = round(26.0 + random.uniform(0.5, 3.5), 1)
            room_temp = round(22.0 + random.uniform(0.2, 2.5), 1)
            grounding = round(random.uniform(0.8, 2.0), 2)
            freq = round(50.0 + random.uniform(-0.08, 0.08), 2)
            runtime_min = int(round(90.0 * (battery_pct / 100.0) * (50.0 / max(1.0, load_pct))))

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

        return inserted_count
    except Exception as ex:
        print(f"[!] Error saat simulasi UPS: {ex}")
        return 0

def simulate_pac_monitoring(cursor, now):
    """Menghasilkan log telemetri Precision Air Conditioning (PAC) lengkap."""
    try:
        cursor.execute("""
            SELECT d.id, d.device_name
            FROM tbl_m_devices d
            JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
            WHERE a.type_name = 'PAC' AND d.is_active = 1
        """)
        devices = cursor.fetchall()
        if not devices:
            return 0

        inserted_count = 0
        for dev in devices:
            dev_id = str(dev['id'])
            log_id = str(uuid.uuid4())
            room_temp = round(21.0 + random.uniform(-1.5, 1.8), 1)
            room_humid = round(52.0 + random.uniform(-3.5, 3.5), 1)
            temp_sp = 22.0
            humid_sp = 50.0
            valve_open = round(random.uniform(40.0, 75.0), 1)
            water_in = round(7.2 + random.uniform(0.0, 1.2), 1)
            water_out = round(12.5 + random.uniform(0.0, 1.5), 1)
            cool_demand = round(random.uniform(45.0, 70.0), 1)
            dehum_demand = round(random.uniform(0.0, 8.0), 1)
            humid_demand = round(random.uniform(0.0, 5.0), 1)
            comp1 = 1
            comp2 = 1 if random.random() > 0.4 else 0
            comp3 = 0

            cursor.execute("""
                INSERT INTO tbl_t_pac_logs (
                    id, device_id, device_time, room_temp, room_humid,
                    temp_setpoint, humid_setpoint, valve_open, water_in_temp, water_out_temp,
                    cool_demand, heat_demand, dehum_demand, humid_demand,
                    unit_id, supervisory_status, unit_status_code,
                    comp1, comp2, comp3, heater1, heater2, active_alarms
                ) VALUES (
                    %s, %s, %s, %s, %s,
                    %s, %s, %s, %s, %s,
                    %s, 0.0, %s, %s,
                    1, 0, 0,
                    %s, %s, %s, 0, 0, 'Normal'
                )
            """, (
                log_id, dev_id, now, room_temp, room_humid,
                temp_sp, humid_sp, valve_open, water_in, water_out,
                cool_demand, dehum_demand, humid_demand,
                comp1, comp2, comp3
            ))
            inserted_count += 1

        return inserted_count
    except Exception as ex:
        print(f"[!] Error saat simulasi PAC: {ex}")
        return 0

def simulate_sensor_monitoring(cursor, now):
    """Menghasilkan log Sensor Suhu & Kelembaban Ruangan."""
    try:
        cursor.execute("""
            SELECT d.id, d.device_name
            FROM tbl_m_devices d
            JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
            WHERE a.type_name = 'SENSOR' AND d.is_active = 1
        """)
        devices = cursor.fetchall()
        if not devices:
            return 0

        inserted_count = 0
        for dev in devices:
            dev_id = str(dev['id'])
            log_id = str(uuid.uuid4())
            temp = round(21.5 + random.uniform(-1.2, 1.5), 1)
            humid = round(52.5 + random.uniform(-2.5, 3.0), 1)

            cursor.execute("""
                INSERT INTO tbl_t_sensor_logs (id, device_id, device_time, temperature, humidity)
                VALUES (%s, %s, %s, %s, %s)
            """, (log_id, dev_id, now, temp, humid))
            inserted_count += 1

        return inserted_count
    except Exception as ex:
        print(f"[!] Error saat simulasi Sensor: {ex}")
        return 0

def simulate_cctv_monitoring(cursor, now):
    """Menghasilkan status availability CCTV."""
    try:
        cursor.execute("""
            SELECT d.id, d.device_name
            FROM tbl_m_devices d
            JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
            WHERE a.type_name = 'CCTV' AND d.is_active = 1
            ORDER BY d.device_name
        """)
        devices = cursor.fetchall()
        if not devices:
            return 0

        inserted_count = 0
        for idx, dev in enumerate(devices):
            dev_id = str(dev['id'])
            log_id = str(uuid.uuid4())
            # Simulasikan 1 unit (index 3) berstatus OFFLINE untuk memvalidasi alarm CCTV
            status = 'OFFLINE' if idx == 3 else 'ONLINE'

            cursor.execute("""
                INSERT INTO tbl_t_cctv_logs (id, device_id, device_time, status)
                VALUES (%s, %s, %s, %s)
            """, (log_id, dev_id, now, status))
            inserted_count += 1

        return inserted_count
    except Exception as ex:
        print(f"[!] Error saat simulasi CCTV: {ex}")
        return 0

def simulate_bts_and_tracking(cursor, now, step_counter):
    """Menghasilkan log BTS (Genset, Tower) dan Fleet Tracking."""
    try:
        # 1. Genset
        cursor.execute("""
            SELECT d.id FROM tbl_m_devices d
            JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
            WHERE a.type_name = 'Genset' AND d.is_active = 1
        """)
        gensets = cursor.fetchall()
        for g in gensets:
            v = round(random.uniform(218.0, 224.0), 1)
            i = round(random.uniform(10.0, 25.0), 1)
            p_kw = round(v * i / 1000.0, 2)
            fuel = round(random.uniform(60.0, 95.0), 1)
            cursor.execute("""
                INSERT INTO tbl_t_genset_logs (
                    id, device_id, device_time, voltage, [current], power_kw,
                    fuel_level, frequency, power_factor, energy_kwh,
                    is_acknowledged, created_at, updated_at
                ) VALUES (
                    %s, %s, %s, %s, %s, %s,
                    %s, 50.0, 0.95, %s,
                    0, %s, %s
                )
            """, (str(uuid.uuid4()), str(g['id']), now, v, i, p_kw, fuel, round(random.uniform(100.0, 300.0), 1), now, now))

        # 2. Tower
        cursor.execute("""
            SELECT d.id FROM tbl_m_devices d
            JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
            WHERE a.type_name = 'Tower' AND d.is_active = 1
        """)
        towers = cursor.fetchall()
        for t in towers:
            cursor.execute("""
                INSERT INTO tbl_t_tower_logs (
                    id, device_id, device_time, tilt_x, tilt_y, ground_resistance,
                    spd_count, lightning_current, wind_speed, rainfall, temperature,
                    dust_level, luminosity, spd_status, is_acknowledged, created_at, updated_at
                ) VALUES (
                    %s, %s, %s, %s, %s, %s,
                    0, 0.0, %s, %s, %s,
                    %s, %s, 'OK', 0, %s, %s
                )
            """, (
                str(uuid.uuid4()), str(t['id']), now,
                round(random.uniform(-0.15, 0.15), 2),
                round(random.uniform(-0.15, 0.15), 2),
                round(random.uniform(1.0, 1.8), 2),
                round(random.uniform(4.0, 20.0), 1),
                round(random.uniform(0.0, 5.0), 1),
                round(random.uniform(28.0, 32.0), 1),
                round(random.uniform(15.0, 35.0), 1),
                round(random.uniform(200.0, 600.0), 1),
                now, now
            ))

        # 3. Fleet Tracking
        cursor.execute("""
            SELECT d.id, s.latitude, s.longitude
            FROM tbl_m_devices d
            JOIN tbl_r_asset_types a ON d.asset_type_id = a.id
            JOIN tbl_m_sites s ON s.id = d.site_id
            WHERE a.type_name = 'Tracking' AND d.is_active = 1
        """)
        fleet_units = cursor.fetchall()
        for idx, u in enumerate(fleet_units):
            b_lat = float(u['latitude'] or -1.5)
            b_lng = float(u['longitude'] or 116.5)
            angle = (step_counter * 10 + idx * 40) % 360
            rad = math.radians(angle)
            c_lat = round(b_lat + math.sin(rad) * 0.012, 6)
            c_lng = round(b_lng + math.cos(rad) * 0.012, 6)
            speed = round(random.uniform(20.0, 60.0), 1)
            rsrp = random.randint(-95, -75)
            sig = 'Excellent' if rsrp >= -85 else 'Good'

            cursor.execute("""
                INSERT INTO tbl_t_fleet_tracking_logs (
                    id, device_id, device_time, latitude, longitude, speed_kmh,
                    heading_degree, altitude, gps_status, satellite_in_use,
                    network_rsrp, network_sinr, network_rssi, network_rsrq,
                    signal_quality, created_at
                ) VALUES (
                    %s, %s, %s, %s, %s, %s,
                    %s, %s, 'Fix', 12,
                    %s, %s, %s, %s,
                    %s, %s
                )
            """, (
                str(uuid.uuid4()), str(u['id']), now, c_lat, c_lng, int(speed),
                int((angle + 90) % 360), round(random.uniform(60.0, 110.0), 1),
                rsrp, round(random.uniform(10.0, 22.0), 1),
                random.randint(-80, -60), random.randint(-12, -8),
                sig, now
            ))

    except Exception as ex:
        print(f"[!] Error saat simulasi BTS/Tracking: {ex}")

def simulate_asset_pulse(conn_itassettrack):
    """Menghasilkan status heartbeat laptop/PC untuk db_itassettrack."""
    try:
        cursor = conn_itassettrack.cursor(as_dict=True)
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
    print("  InfraHub MULTI-SUBSYSTEM TELEMETRY GENERATOR SERVICE")
    print("  (DC-Infrastructure: UPS, PAC, Sensor, CCTV + BTS & AssetTrack)")
    print("=" * 65)

    if not wait_for_database("db_infrahub") or not wait_for_database("db_itassettrack"):
        print("[!] Gagal terhubung ke database. Service generator berhenti.")
        sys.exit(1)

    print(f"[+] Service aktif! Menginjeksi data telemetri berkala setiap {INTERVAL_SECONDS} detik...")

    step_counter = 0
    while True:
        now = get_now()
        try:
            with get_connection("db_infrahub") as conn_infra:
                cur = conn_infra.cursor(as_dict=True)
                ups_count = simulate_ups_monitoring(cur, now)
                pac_count = simulate_pac_monitoring(cur, now)
                sns_count = simulate_sensor_monitoring(cur, now)
                cctv_count = simulate_cctv_monitoring(cur, now)
                simulate_bts_and_tracking(cur, now, step_counter)
                print(f"[{now.strftime('%H:%M:%S')}] [DC-Infra] Injeksi Telemetri: {ups_count} UPS, {pac_count} PAC, {sns_count} Sensor, {cctv_count} CCTV.")

            with get_connection("db_itassettrack") as conn_itasset:
                simulate_asset_pulse(conn_itasset)

        except Exception as e:
            print(f"[!] Exception pada loop utama: {e}")

        step_counter += 1
        time.sleep(INTERVAL_SECONDS)

if __name__ == "__main__":
    main()
