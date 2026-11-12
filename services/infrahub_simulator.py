"""
InfraHub Integrated Telemetry Simulator
Writes BTS (Tower, Genset) + DC/UPS (UPS, Sensor, PAC, CCTV) + LTE Tracking logs
every TICK_INTERVAL_SECONDS to db_infrahub.

Usage:
    python infrahub_simulator.py

Requirements:
    pip install pyodbc
    ODBC Driver 17 for SQL Server must be installed.

Schema notes (actual DB):
  tbl_r_asset_types  : id, type_name
  tbl_t_genset_logs  : [current] is reserved keyword — must quote
  tbl_t_tower_logs   : is_acknowledged (NOT NULL), updated_at (NOT NULL)
  tbl_t_ups_logs     : ups_status, voltage_in_a/b/c, voltage_out_a/b/c, current_out_a, load_percent, battery_percent, battery_runtime_min, ambient_temp
  tbl_t_sensor_logs  : id, device_id, device_time, temperature, humidity
  tbl_t_pac_logs     : id, device_id, device_time, temp_setpoint, room_temp, room_humid, unit_status_code
  tbl_t_cctv_logs    : id, device_id, device_time, status
  tbl_t_fleet_tracking_logs : latitude, longitude, speed_kmh, heading_degree, altitude, gps_status, satellite_in_use, network_rsrp/sinr/rssi/rsrq, signal_quality
"""
import pyodbc
import uuid
import math
import random
import time
from datetime import datetime

CONN_STR = (
    "Driver={ODBC Driver 17 for SQL Server};"
    "Server=localhost;"
    "Database=db_infrahub;"
    "Trusted_Connection=yes;"
    "TrustServerCertificate=yes;"
)
TICK_INTERVAL_SECONDS = 10


def get_db():
    return pyodbc.connect(CONN_STR, autocommit=True)


def simulate_bts_and_dc(cursor):
    """Write one telemetry row per active device (BTS + DC sub-systems)."""
    cursor.execute("""
        SELECT d.id, at.type_name
        FROM dbo.tbl_m_devices d
        JOIN dbo.tbl_r_asset_types at ON at.id = d.asset_type_id
        WHERE d.is_active = 1
    """)
    devices = [(str(r[0]), r[1]) for r in cursor.fetchall()]
    now = datetime.now()

    for dev_id, asset_type in devices:
        try:
            if asset_type == 'Genset':
                v = round(random.uniform(218.0, 224.0), 1)
                i = round(random.uniform(10.0, 25.0), 1)
                cursor.execute("""
                    INSERT INTO dbo.tbl_t_genset_logs
                        (id, device_id, device_time, voltage, [current], power_kw,
                         fuel_level, frequency, power_factor, energy_kwh,
                         is_acknowledged, created_at, updated_at)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0, GETDATE(), GETDATE())
                """, (str(uuid.uuid4()), dev_id, now, v, i, round(v * i / 1000, 2),
                      round(random.uniform(55.0, 90.0), 1), 50.0, 0.95,
                      round(random.uniform(100.0, 300.0), 1)))

            elif asset_type == 'Tower':
                cursor.execute("""
                    INSERT INTO dbo.tbl_t_tower_logs
                        (id, device_id, device_time,
                         tilt_x, tilt_y, ground_resistance, spd_count,
                         lightning_current, wind_speed, rainfall, temperature,
                         dust_level, luminosity, spd_status,
                         is_acknowledged, created_at, updated_at)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'OK', 0, GETDATE(), GETDATE())
                """, (str(uuid.uuid4()), dev_id, now,
                      round(random.uniform(-0.3, 0.3), 2),
                      round(random.uniform(-0.3, 0.3), 2),
                      round(random.uniform(1.0, 2.0), 2), 0, 0.0,
                      round(random.uniform(5.0, 25.0), 1),
                      round(random.uniform(0.0, 8.0), 1),
                      round(random.uniform(27.0, 33.0), 1),
                      round(random.uniform(10.0, 40.0), 1),
                      round(random.uniform(100.0, 700.0), 1)))

            elif asset_type == 'UPS':
                v_a = round(random.uniform(215.0, 225.0), 1)
                i_a = round(random.uniform(5.0, 18.0), 1)
                cursor.execute("""
                    INSERT INTO dbo.tbl_t_ups_logs
                        (id, device_id, device_time, ups_status,
                         voltage_in_a, voltage_in_b, voltage_in_c,
                         voltage_out_a, voltage_out_b, voltage_out_c,
                         current_out_a, load_percent, battery_percent,
                         battery_runtime_min, ambient_temp, created_at)
                    VALUES (?, ?, ?, 'ON LINE', ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, GETDATE())
                """, (str(uuid.uuid4()), dev_id, now,
                      v_a, v_a, v_a, 220.0, 220.0, 220.0, i_a,
                      round(random.uniform(30.0, 65.0), 1),
                      round(random.uniform(96.0, 100.0), 1),
                      random.randint(60, 180),
                      round(random.uniform(26.0, 31.0), 1)))

            elif asset_type == 'SENSOR':
                cursor.execute("""
                    INSERT INTO dbo.tbl_t_sensor_logs (id, device_id, device_time, temperature, humidity)
                    VALUES (?, ?, ?, ?, ?)
                """, (str(uuid.uuid4()), dev_id, now,
                      round(random.uniform(20.0, 26.0), 1),
                      round(random.uniform(45.0, 65.0), 1)))

            elif asset_type == 'PAC':
                cursor.execute("""
                    INSERT INTO dbo.tbl_t_pac_logs
                        (id, device_id, device_time, temp_setpoint, room_temp, room_humid, unit_status_code)
                    VALUES (?, ?, ?, 22.0, ?, ?, 0)
                """, (str(uuid.uuid4()), dev_id, now,
                      round(random.uniform(19.0, 23.0), 1),
                      round(random.uniform(50.0, 60.0), 1)))

            elif asset_type == 'CCTV':
                cursor.execute("""
                    INSERT INTO dbo.tbl_t_cctv_logs (id, device_id, device_time, status)
                    VALUES (?, ?, ?, 'ONLINE')
                """, (str(uuid.uuid4()), dev_id, now))

        except Exception as e:
            print(f"  [WARN] {asset_type} {dev_id[:8]}: {e}")


def simulate_fleet_movement(cursor, step_counter):
    """Move LTE tracking units in an orbit around their site base coordinates."""
    cursor.execute("""
        SELECT d.id, s.latitude, s.longitude
        FROM dbo.tbl_m_devices d
        JOIN dbo.tbl_r_asset_types at ON at.id = d.asset_type_id
        JOIN dbo.tbl_m_sites s ON s.id = d.site_id
        WHERE at.type_name = 'Tracking' AND d.is_active = 1
    """)
    units = [(str(r[0]), float(r[1] or -1.5), float(r[2] or 116.5)) for r in cursor.fetchall()]
    now = datetime.now()

    for idx, (dev_id, b_lat, b_lng) in enumerate(units):
        angle = (step_counter * 10 + idx * 45) % 360
        rad = math.radians(angle)
        c_lat = round(b_lat + math.sin(rad) * 0.015, 6)
        c_lng = round(b_lng + math.cos(rad) * 0.015, 6)
        speed = round(random.uniform(15.0, 65.0), 1)
        rsrp = random.randint(-105, -75)
        sig = 'Excellent' if rsrp >= -85 else ('Good' if rsrp >= -95 else 'Fair')

        try:
            cursor.execute("""
                INSERT INTO dbo.tbl_t_fleet_tracking_logs
                    (id, device_id, device_time, latitude, longitude, speed_kmh, heading_degree,
                     altitude, gps_status, satellite_in_use,
                     network_rsrp, network_sinr, network_rssi, network_rsrq,
                     signal_quality, created_at)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'Fix', 12, ?, ?, ?, ?, ?, GETDATE())
            """, (str(uuid.uuid4()), dev_id, now, c_lat, c_lng, speed,
                  round((angle + 90) % 360, 1),
                  round(random.uniform(50.0, 120.0), 1),
                  rsrp, round(random.uniform(5.0, 25.0), 1),
                  random.randint(-85, -55), random.randint(-14, -6), sig))
        except Exception as e:
            print(f"  [WARN] Tracking {dev_id[:8]}: {e}")


def main():
    print(f"InfraHub Simulator started — tick every {TICK_INTERVAL_SECONDS}s")
    step = 0
    while True:
        try:
            conn = get_db()
            cursor = conn.cursor()
            simulate_bts_and_dc(cursor)
            simulate_fleet_movement(cursor, step)
            cursor.close()
            conn.close()
            step += 1
            print(f"[{datetime.now().strftime('%H:%M:%S')}] tick #{step}")
        except Exception as e:
            print(f"[ERROR] tick #{step}: {e}")
        time.sleep(TICK_INTERVAL_SECONDS)


if __name__ == '__main__':
    main()
