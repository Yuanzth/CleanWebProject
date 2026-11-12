@echo off
echo ========================================================
echo   MENJALANKAN PYTHON DUMMY TELEMETRY GENERATOR (LOKAL)
echo ========================================================
echo.
echo Memastikan dependensi Python terpasang...
python -m pip install -q -r "data-generator\requirements.txt"
echo.
echo Menjalankan generator telemetri (Tekan Ctrl+C untuk berhenti)...
python "data-generator\generator.py"
pause
