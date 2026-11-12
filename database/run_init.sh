#!/bin/bash
echo "[*] Menunggu SQL Server siap menerima koneksi..."

SQLCMD_BIN="/opt/mssql-tools18/bin/sqlcmd"
if [ ! -f "$SQLCMD_BIN" ]; then
    SQLCMD_BIN="/opt/mssql-tools/bin/sqlcmd"
fi

for i in {1..60}; do
    $SQLCMD_BIN -S sqlserver -U sa -P "AdminPass123!" -C -Q "SELECT 1" > /dev/null 2>&1
    if [ $? -eq 0 ]; then
        echo "[+] SQL Server siap!"
        break
    fi
    echo "[-] Menunggu SQL Server... ($i/60)"
    sleep 2
done

echo "[*] Menjalankan 01_init_databases.sql..."
$SQLCMD_BIN -S sqlserver -U sa -P "AdminPass123!" -C -i /init-scripts/01_init_databases.sql

echo "[*] Menjalankan 02_restore_itassettrack.sql..."
$SQLCMD_BIN -S sqlserver -U sa -P "AdminPass123!" -C -i /init-scripts/02_restore_itassettrack.sql

echo "[*] Menjalankan 03_restore_monitoring_master.sql..."
$SQLCMD_BIN -S sqlserver -U sa -P "AdminPass123!" -C -i /init-scripts/03_restore_monitoring_master.sql

echo "[*] Menjalankan 04_create_procedures_monitoring.sql..."
$SQLCMD_BIN -S sqlserver -U sa -P "AdminPass123!" -C -i /init-scripts/04_create_procedures_monitoring.sql

echo "[+] Seluruh Database dan Stored Procedures berhasil diinisialisasi!"
