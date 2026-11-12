// ── Sidebar submenu toggle
function toggleSubmenu(btn) {
    btn.classList.toggle('open');
    const submenu = btn.nextElementSibling;
    if (submenu) submenu.classList.toggle('open');
}

// ── Toggle button groups (single select)
document.addEventListener('DOMContentLoaded', function () {

    // Mobile Off-Canvas Sidebar Handler
    initMobileSidebar();

    // Generic toggle groups
    document.querySelectorAll('[data-toggle-group]').forEach(group => {
        const btns = group.querySelectorAll('.btn-toggle');
        btns.forEach(btn => {
            btn.addEventListener('click', () => {
                btns.forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                const target = group.dataset.targetInput;
                if (target) {
                    const input = document.querySelector(target);
                    if (input) input.value = btn.dataset.value || btn.textContent.trim();
                }
            });
        });
    });

    // OS buttons
    document.querySelectorAll('.os-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const parent = btn.closest('.os-group');
            parent.querySelectorAll('.os-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            const input = parent.nextElementSibling;
            if (input && input.tagName === 'INPUT') input.value = btn.dataset.value;
        });
    });

    // Hardware items (multi select)
    document.querySelectorAll('.hw-item').forEach(item => {
        item.addEventListener('click', () => {
            item.classList.toggle('active');
            updateHardwareInput();
        });
    });

    function updateHardwareInput() {
        const active = Array.from(document.querySelectorAll('.hw-item.active'))
                            .map(i => i.dataset.value);
        const input = document.getElementById('additionalHardwareInput');
        if (input) input.value = active.join(',');
    }

    // Kondisi radio (Baik / Rusak)
    document.querySelectorAll('.kondisi-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const group = btn.closest('.kondisi-group');
            group.querySelectorAll('.kondisi-btn').forEach(b => {
                b.style.opacity = '0.45';
                b.style.fontWeight = '400';
            });
            btn.style.opacity = '1';
            btn.style.fontWeight = '600';

            const radio = btn.querySelector('input[type=radio]');
            if (radio) radio.checked = true;
        });
    });

    // Init signature pads
    document.querySelectorAll('[data-signature]').forEach(canvas => {
        if (canvas.getContext) {
            const pad = new SignaturePad(canvas, {
                backgroundColor: 'rgba(248,249,250,0)',
                penColor: '#1565C0'
            });
            canvas.signaturePad = pad;

            const clearBtn = canvas.closest('.signature-box')?.querySelector('.btn-clear-sig');
            if (clearBtn) {
                clearBtn.addEventListener('click', () => pad.clear());
            }

            // Resize canvas properly
            function resizeCanvas() {
                const ratio = Math.max(window.devicePixelRatio || 1, 1);
                canvas.width = canvas.offsetWidth * ratio;
                canvas.height = canvas.offsetHeight * ratio;
                canvas.getContext('2d').scale(ratio, ratio);
                pad.clear();
            }
            window.addEventListener('resize', resizeCanvas);
            resizeCanvas();
        }
    });

    // Tab switching
    document.querySelectorAll('[data-tab-target]').forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.dataset.tabTarget;
            const container = btn.closest('[data-tab-container]') || document;

            // Update tab buttons
            const allBtns = btn.closest('.tab-nav')?.querySelectorAll('[data-tab-target]');
            allBtns?.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');

            // Show/hide panels
            container.querySelectorAll('[data-tab-panel]').forEach(panel => {
                panel.style.display = panel.dataset.tabPanel === targetId ? '' : 'none';
            });
        });
    });

    // Auto-fill SN listener
    const snInputs = document.querySelectorAll('input[name="SerialNumber"], input[name="serialNumber"]');
    snInputs.forEach(input => {
        input.addEventListener('change', function () {
            if (this.value.trim() !== '') {
                lookupAssetBySN(this.value.trim());
            }
        });
    });

});

// ── Toast notification
function showToast(message, type = 'success') {
    const container = document.getElementById('toastContainer');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `toast ${type === 'error' ? 'error' : ''}`;
    toast.innerHTML = `<i class="bi bi-${type === 'error' ? 'x-circle' : 'check-circle'}" style="color:${type === 'error' ? '#C62828' : '#2E7D32'};margin-right:8px;"></i>${message}`;
    container.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transition = 'opacity 0.3s ease';
        setTimeout(() => toast.remove(), 300);
    }, 3500);
}

// ── Auto-Fill Asset & Employee by Serial Number (SN) Lookup
function lookupAssetBySN(sn) {
    if (!sn) return;
    fetch(`/api/asset/lookup-by-sn/${encodeURIComponent(sn)}`)
        .then(response => {
            if (!response.ok) throw new Error("Aset tidak ditemukan.");
            return response.json();
        })
        .then(data => {
            // Helper to fill input if element exists
            const fillField = (idOrName, val) => {
                const el = document.getElementById(idOrName) || document.querySelector(`[name="${idOrName}"]`);
                if (el && val) el.value = val;
            };

            fillField('NamaUser', data.assignedTo);
            fillField('AssignedTo', data.assignedTo);
            fillField('NRP', data.nrp);
            fillField('Site', data.site);
            fillField('Division', data.division);
            fillField('Department', data.department);
            fillField('ComputerName', data.computerName);
            fillField('DeviceType', data.deviceType);
            fillField('Brand', data.brand);
            fillField('Series', data.model);
            fillField('Model', data.model);
            fillField('Ram', data.ram);
            fillField('Storage', data.storage);
            fillField('Processor', data.processor);
            fillField('OperatingSystem', data.operatingSystem);

            showToast(`Auto-fill berhasil untuk SN: ${data.serialNumber} (${data.assignedTo || 'Unassigned'})`, 'success');
        })
        .catch(err => {
            console.log('Lookup SN error:', err.message);
        });
}

// ── Form submit handler (save signature to hidden input)
function collectSignatures(formId) {
    const form = document.getElementById(formId);
    if (!form) return;
    form.querySelectorAll('[data-signature]').forEach(canvas => {
        if (canvas.signaturePad && !canvas.signaturePad.isEmpty()) {
            const hiddenInput = form.querySelector(`[name="${canvas.dataset.signatureTarget}"]`);
            if (hiddenInput) hiddenInput.value = canvas.signaturePad.toDataURL();
        }
    });
}

// ── Mobile Off-Canvas Sidebar Toggle ──
function initMobileSidebar() {
    const toggleBtn = document.getElementById('sidebarToggle');
    const backdrop = document.getElementById('sidebarBackdrop');
    const sidebar = document.querySelector('.sidebar');

    function openSidebar() {
        document.body.classList.add('sidebar-open');
        if (sidebar) sidebar.classList.add('sidebar-open');
        if (backdrop) backdrop.classList.add('show');
    }

    function closeSidebar() {
        document.body.classList.remove('sidebar-open');
        if (sidebar) sidebar.classList.remove('sidebar-open');
        if (backdrop) backdrop.classList.remove('show');
    }

    if (toggleBtn) {
        toggleBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            if (document.body.classList.contains('sidebar-open')) {
                closeSidebar();
            } else {
                openSidebar();
            }
        });
    }

    if (backdrop) {
        backdrop.addEventListener('click', closeSidebar);
    }

    if (sidebar) {
        sidebar.querySelectorAll('a').forEach(link => {
            link.addEventListener('click', function () {
                if (window.innerWidth < 992) {
                    closeSidebar();
                }
            });
        });
    }

    window.addEventListener('resize', function () {
        if (window.innerWidth >= 992) {
            closeSidebar();
        }
    });
}
