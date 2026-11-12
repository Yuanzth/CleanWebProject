# -*- mode: python ; coding: utf-8 -*-
import os

block_cipher = None

spec_root = os.path.dirname(os.path.abspath(SPEC))
ico_path = os.path.join(spec_root, 'itassettrack.ico')
version_path = os.path.join(spec_root, 'version_info.txt')

datas = []
if os.path.exists(ico_path):
    datas.append((ico_path, '.'))

a = Analysis(
    ['agent_service.py'],
    pathex=[spec_root],
    binaries=[],
    datas=datas,
    hiddenimports=[
        'psutil',
        'requests',
        'urllib3',
        'pystray',
        'PIL',
        'PIL._imaging',
        'PIL.Image',
        'PIL.ImageDraw',
        'PIL.ImageTk',
        'tkinter',
        '_tkinter',
        'sqlite3'
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='itassettrack_pulse',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=ico_path if os.path.exists(ico_path) else None,
    version=version_path if os.path.exists(version_path) else None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=False,
    upx_exclude=[],
    name='itassettrack_pulse',
)
