// COLOR PALETTE
const UPS_RPT_COLORS = [
    { border: '#60a5fa', bg: 'rgba(96,165,250,0.10)' },
    { border: '#4ade80', bg: 'rgba(74,222,128,0.10)' },
    { border: '#fb923c', bg: 'rgba(251,146,60,0.10)' },
    { border: '#a78bfa', bg: 'rgba(167,139,250,0.10)' },
    { border: '#f472b6', bg: 'rgba(244,114,182,0.10)' },
    { border: '#34d399', bg: 'rgba(52,211,153,0.10)' },
    { border: '#fbbf24', bg: 'rgba(251,191,36,0.10)' },
    { border: '#f87171', bg: 'rgba(248,113,113,0.10)' },
    { border: '#38bdf8', bg: 'rgba(56,189,248,0.10)' },
    { border: '#c084fc', bg: 'rgba(192,132,252,0.10)' },
];

function upsRptColor(idx) {
    return UPS_RPT_COLORS[idx % UPS_RPT_COLORS.length];
}

// CHART REGISTRY
const _charts = {};

if (typeof ChartDataLabels !== 'undefined') {
    Chart.register(ChartDataLabels);
    Chart.defaults.plugins.datalabels.display = false; // Disable globally by default
}

function destroyChart(id) {
    if (_charts[id]) { _charts[id].destroy(); delete _charts[id]; }
}

// CHART.JS DEFAULTS
const initialTheme = localStorage.getItem('ups-theme') || 'dark';
Chart.defaults.font.family = "'Inter', 'Segoe UI', system-ui, sans-serif";
Chart.defaults.color = initialTheme === 'light' ? '#64748b' : '#8b9ab7';

// THEME CHANGE LISTENER
window.addEventListener('ups-theme-changed', function (e) {
    const theme = e.detail.theme;
    const isLight = theme === 'light';

    Chart.defaults.color = isLight ? '#64748b' : '#8b9ab7';

    Object.keys(_charts).forEach(function (id) {
        const chart = _charts[id];
        if (!chart) return;

        // Update scales
        if (chart.options.scales) {
            if (chart.options.scales.x) {
                if (chart.options.scales.x.grid) chart.options.scales.x.grid.color = isLight ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.07)';
                if (chart.options.scales.x.ticks) chart.options.scales.x.ticks.color = isLight ? '#64748b' : '#4b5a72';
                if (chart.options.scales.x.border) chart.options.scales.x.border.color = isLight ? 'rgba(148,163,184,0.2)' : 'rgba(148,163,184,0.1)';
            }
            if (chart.options.scales.y) {
                if (chart.options.scales.y.grid) chart.options.scales.y.grid.color = isLight ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.07)';
                if (chart.options.scales.y.ticks) chart.options.scales.y.ticks.color = isLight ? '#64748b' : '#4b5a72';
                if (chart.options.scales.y.border) chart.options.scales.y.border.color = isLight ? 'rgba(148,163,184,0.2)' : 'rgba(148,163,184,0.1)';
            }
        }

        // Update plugins
        if (chart.options.plugins) {
            if (chart.options.plugins.legend && chart.options.plugins.legend.labels) {
                chart.options.plugins.legend.labels.color = isLight ? '#475569' : '#8b9ab7';
            }
            if (chart.options.plugins.tooltip) {
                chart.options.plugins.tooltip.backgroundColor = isLight ? 'rgba(255,255,255,0.98)' : 'rgba(8,11,24,0.96)';
                chart.options.plugins.tooltip.borderColor = isLight ? 'rgba(148,163,184,0.25)' : 'rgba(148,163,184,0.18)';
                chart.options.plugins.tooltip.titleColor = isLight ? '#0f172a' : '#eef4ff';
                chart.options.plugins.tooltip.bodyColor = isLight ? '#475569' : '#8b9ab7';
            }
        }

        chart.update();
    });
});

// THRESHOLD DATASET HELPER
function thresholdDataset(label, value, color, labelCount) {
    return {
        label: label,
        data: Array(labelCount).fill(value),
        borderColor: color,
        backgroundColor: 'transparent',
        borderWidth: 1.5,
        borderDash: [6, 4],
        pointRadius: 0,
        pointHoverRadius: 0,
        fill: false,
        tension: 0,
        spanGaps: true,
        // Hide from legend click interaction
        order: 99,
    };
}

// build line chart
function buildLineChart(canvasId, trendData, unit, thresholds) {
    unit = unit || 'V';
    thresholds = thresholds || [];
    destroyChart(canvasId);

    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    const isEmpty = !trendData || !trendData.labels || trendData.labels.length === 0
        || !trendData.devices || trendData.devices.length === 0;

    if (isEmpty) {
        renderNoData(canvas);
        return;
    }

    const labels = trendData.labels;
    const devices = trendData.devices;

    let hasAnyData = false;
    for (let i = 0; i < devices.length; i++) {
        if (devices[i].values && devices[i].values.some(function (v) { return v !== null && v !== undefined; })) {
            hasAnyData = true;
            break;
        }
    }

    if (!hasAnyData) {
        renderNoData(canvas);
        return;
    }


    var aggFilter = document.getElementById('upsRptAggregationFilter');
    var isActual = aggFilter ? (aggFilter.value === 'actual') : false;

    // Device datasets
    const datasets = devices.map(function (dev, i) {
        const c = upsRptColor(i);
        const rawValues = dev.values || [];
        const values = rawValues.map(function (v) {
            return (v !== null && v !== undefined && !isNaN(v)) ? Number(v) : 0;
        });
        return {
            label: dev.name,
            data: values,
            borderColor: c.border,
            backgroundColor: c.bg,
            borderWidth: isActual ? 1.5 : 2,
            pointRadius: isActual ? 2 : (labels.length > 30 ? 0 : 3),
            pointHoverRadius: isActual ? 4 : 5,
            fill: false,
            tension: 0.35,
            spanGaps: true,
            order: i,
        };
    });

    // Add threshold lines as datasets (no plugin needed)
    thresholds.forEach(function (t) {
        datasets.push(thresholdDataset(t.label, t.value, t.color, labels.length));
    });

    const isL = (localStorage.getItem('ups-theme') || 'dark') === 'light';

    _charts[canvasId] = new Chart(canvas, {
        type: 'line',
        data: { labels: labels, datasets: datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: {
                    display: true,
                    position: 'top',
                    labels: {
                        color: isL ? '#475569' : '#8b9ab7',
                        boxWidth: 10,
                        boxHeight: 10,
                        padding: 14,
                        font: { size: 11, weight: '600' },
                        usePointStyle: true,
                        pointStyle: 'circle',
                        // Hide threshold datasets from legend
                        filter: function (item) {
                            return item.datasetIndex < devices.length;
                        },
                    },
                },
                tooltip: {
                    backgroundColor: isL ? 'rgba(255,255,255,0.98)' : 'rgba(8,11,24,0.96)',
                    borderColor: isL ? 'rgba(148,163,184,0.25)' : 'rgba(148,163,184,0.18)',
                    borderWidth: 1,
                    titleColor: isL ? '#0f172a' : '#eef4ff',
                    bodyColor: isL ? '#475569' : '#8b9ab7',
                    padding: 10,
                    callbacks: {
                        label: function (ctx) {
                            if (ctx.datasetIndex >= devices.length) return null; // hide threshold in tooltip
                            var v = ctx.parsed.y;
                            return ' ' + ctx.dataset.label + ': ' + (v !== null ? v + ' ' + unit : '-');
                        },
                    },
                },
            },
            scales: {
                x: {
                    grid: { color: isL ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.07)', drawTicks: false },
                    ticks: { color: isL ? '#64748b' : '#4b5a72', font: { size: 10 }, maxRotation: 0 },
                    border: { color: isL ? 'rgba(148,163,184,0.2)' : 'rgba(148,163,184,0.1)' },
                },
                y: {
                    grid: { color: isL ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.07)', drawTicks: false },
                    ticks: { color: isL ? '#64748b' : '#4b5a72', font: { size: 10 } },
                    border: { color: isL ? 'rgba(148,163,184,0.2)' : 'rgba(148,163,184,0.1)' },
                },
            },
        },
    });
}

// build bar chart & device stat cards
function renderComparisonDeviceCards(compData) {
    var container = document.getElementById('comparisonDeviceCards');
    if (!container) return;

    if (!compData || !compData.labels || compData.labels.length === 0) {
        container.innerHTML = '';
        return;
    }

    var html = '';
    for (var i = 0; i < compData.labels.length; i++) {
        var label = compData.labels[i] || 'Device ' + (i + 1);
        var inVal = compData.input && compData.input[i] !== null && compData.input[i] !== undefined ? compData.input[i] : null;
        var outVal = compData.output && compData.output[i] !== null && compData.output[i] !== undefined ? compData.output[i] : null;

        var inStatus = 'normal';
        if (inVal !== null && inVal > 0) {
            if (inVal < 210 || inVal > 230) inStatus = 'crit';
            else if (inVal < 217 || inVal > 223) inStatus = 'warn';
        }

        var outStatus = 'normal';
        if (outVal !== null && outVal > 0) {
            if (outVal < 215 || outVal > 225) outStatus = 'crit';
            else if (outVal < 217 || outVal > 223) outStatus = 'warn';
        }

        var deltaStr = '—';
        var deltaClass = 'ups-comp-badge--good';
        var deltaStatus = 'normal';
        if (inVal !== null && outVal !== null) {
            var delta = Math.round((outVal - inVal) * 10) / 10;
            deltaStr = (delta > 0 ? '+' : '') + delta + ' V';
            if (Math.abs(delta) > 5) {
                deltaClass = 'ups-comp-badge--crit';
                deltaStatus = 'crit';
            } else if (Math.abs(delta) > 3) {
                deltaClass = 'ups-comp-badge--warn';
                deltaStatus = 'warn';
            } else {
                deltaClass = 'ups-comp-badge--good';
                deltaStatus = 'normal';
            }
        }

        var overallStatus = 'normal';
        if (inStatus === 'crit' || outStatus === 'crit' || deltaStatus === 'crit') {
            overallStatus = 'crit';
        } else if (inStatus === 'warn' || outStatus === 'warn' || deltaStatus === 'warn') {
            overallStatus = 'warn';
        }

        var cardClass = '';
        var statusBadgeHtml = '';
        if (overallStatus === 'crit') {
            cardClass = 'ups-comp-card--crit';
            statusBadgeHtml = '<span class="ups-comp-badge ups-comp-badge--crit"><i class="fa-solid fa-triangle-exclamation" style="margin-right:3px;"></i>CRITICAL</span>';
        } else if (overallStatus === 'warn') {
            cardClass = 'ups-comp-card--warn';
            statusBadgeHtml = '<span class="ups-comp-badge ups-comp-badge--warn"><i class="fa-solid fa-circle-exclamation" style="margin-right:3px;"></i>WARNING</span>';
        } else {
            statusBadgeHtml = '<span class="ups-comp-badge ups-comp-badge--good"><i class="fa-solid fa-circle-check" style="margin-right:3px;"></i>NORMAL</span>';
        }

        var inChipClass = inStatus !== 'normal' ? ('ups-comp-stat-chip--' + inStatus) : '';
        var outChipClass = outStatus !== 'normal' ? ('ups-comp-stat-chip--' + outStatus) : '';

        var safeLabel = String(label).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

        html += '<div class="ups-comp-card ' + cardClass + '">' +
            '<div class="ups-comp-card__header">' +
                '<div class="ups-comp-card__title">' +
                    '<i class="fa-solid fa-server"></i>' +
                    '<span>' + safeLabel + '</span>' +
                '</div>' +
                '<div style="display:flex; align-items:center; gap:6px;">' +
                    statusBadgeHtml +
                    '<span class="ups-comp-badge ' + deltaClass + '">ΔV: ' + deltaStr + '</span>' +
                '</div>' +
            '</div>' +
            '<div class="ups-comp-card__body">' +
                '<div class="ups-comp-stat-chip ups-comp-stat-chip--in ' + inChipClass + '">' +
                    '<span class="ups-comp-stat-chip__label">AVG Input</span>' +
                    '<span class="ups-comp-stat-chip__value">' + (inVal !== null ? inVal + '<small>V</small>' : '—') + '</span>' +
                '</div>' +
                '<div class="ups-comp-stat-chip ups-comp-stat-chip--out ' + outChipClass + '">' +
                    '<span class="ups-comp-stat-chip__label">AVG Output</span>' +
                    '<span class="ups-comp-stat-chip__value">' + (outVal !== null ? outVal + '<small>V</small>' : '—') + '</span>' +
                '</div>' +
            '</div>' +
        '</div>';
    }

    container.innerHTML = html;
}

function buildComparisonChart(canvasId, compData) {
    renderComparisonDeviceCards(compData);

    destroyChart(canvasId);
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    if (!compData || !compData.labels || compData.labels.length === 0) {
        renderNoData(canvas); return;
    }

    const hasData = (compData.input && compData.input.some(function (v) { return v !== null && v !== undefined; })) ||
        (compData.output && compData.output.some(function (v) { return v !== null && v !== undefined; }));
    if (!hasData) {
        renderNoData(canvas); return;
    }

    const isL = (localStorage.getItem('ups-theme') || 'dark') === 'light';

    _charts[canvasId] = new Chart(canvas, {
        type: 'bar',
        data: {
            labels: compData.labels,
            datasets: [
                {
                    label: 'Input Voltage (avg)',
                    data: compData.input,
                    backgroundColor: 'rgba(96,165,250,0.55)',
                    borderColor: '#60a5fa',
                    borderWidth: 1.5,
                    borderRadius: 6,
                    order: 0,
                },
                {
                    label: 'Output Voltage (avg)',
                    data: compData.output,
                    backgroundColor: 'rgba(74,222,128,0.55)',
                    borderColor: '#4ade80',
                    borderWidth: 1.5,
                    borderRadius: 6,
                    order: 1,
                },
            ],
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: {
                    display: true,
                    position: 'top',
                    labels: { color: isL ? '#475569' : '#8b9ab7', boxWidth: 10, boxHeight: 10, padding: 14, font: { size: 11, weight: '600' } },
                },
                tooltip: {
                    backgroundColor: isL ? 'rgba(255,255,255,0.98)' : 'rgba(8,11,24,0.96)',
                    borderColor: isL ? 'rgba(148,163,184,0.25)' : 'rgba(148,163,184,0.18)',
                    borderWidth: 1,
                    titleColor: isL ? '#0f172a' : '#eef4ff',
                    bodyColor: isL ? '#475569' : '#8b9ab7',
                    padding: 10,
                    callbacks: {
                        label: function (ctx) {
                            return ' ' + ctx.dataset.label + ': ' + (ctx.parsed.y !== null ? ctx.parsed.y + ' V' : '-');
                        },
                    },
                },
            },
            scales: {
                x: { grid: { display: false }, ticks: { color: isL ? '#64748b' : '#4b5a72', font: { size: 10 } }, border: { color: isL ? 'rgba(148,163,184,0.2)' : 'rgba(148,163,184,0.1)' } },
                y: { grid: { color: isL ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.07)', drawTicks: false }, ticks: { color: isL ? '#64748b' : '#4b5a72', font: { size: 10 } }, border: { color: isL ? 'rgba(148,163,184,0.2)' : 'rgba(148,163,184,0.1)' } },
            },
        },
    });
}

// no data fallback
function renderNoData(canvas) {
    // Draw on next frame so canvas has its dimensions
    requestAnimationFrame(function () {
        var ctx = canvas.getContext('2d');
        var w = canvas.offsetWidth || canvas.width || 300;
        var h = canvas.offsetHeight || canvas.height || 180;
        canvas.width = w;
        canvas.height = h;
        ctx.clearRect(0, 0, w, h);
        ctx.fillStyle = 'rgba(148,163,184,0.22)';
        ctx.font = '12px Inter, sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText('Tidak ada data tersedia', w / 2, h / 2);
    });
}

// avg trend
function avgTrend(trendA, trendB, trendC) {
    if (!trendA || !trendA.labels) return null;

    var labels = trendA.labels;

    // Build maps: deviceId â†’ device object
    function toMap(trend) {
        var m = {};
        (trend.devices || []).forEach(function (d) { m[d.id] = d; });
        return m;
    }

    var mA = toMap(trendA), mB = toMap(trendB || {}), mC = toMap(trendC || {});

    // Collect all device IDs
    var allIds = {};
    [mA, mB, mC].forEach(function (m) { Object.keys(m).forEach(function (id) { allIds[id] = true; }); });

    var devices = Object.keys(allIds).map(function (id) {
        var dA = mA[id], dB = mB[id], dC = mC[id];
        var name = (dA || dB || dC).name || 'Unknown';
        var values = labels.map(function (_, i) {
            var vals = [];
            if (dA && dA.values[i] !== null && dA.values[i] !== undefined) vals.push(dA.values[i]);
            if (dB && dB.values[i] !== null && dB.values[i] !== undefined) vals.push(dB.values[i]);
            if (dC && dC.values[i] !== null && dC.values[i] !== undefined) vals.push(dC.values[i]);
            if (vals.length === 0) return null;
            return Math.round((vals.reduce(function (a, b) { return a + b; }, 0) / vals.length) * 10) / 10;
        });
        return { id: id, name: name, values: values };
    });

    devices.sort(function (a, b) { return a.name.localeCompare(b.name); });
    return { labels: labels, devices: devices };
}

function filterTrendDevices(trend, predicate) {
    if (!trend || !trend.labels || !trend.devices) return trend;
    return {
        labels: trend.labels,
        devices: trend.devices.filter(predicate),
    };
}

// helper to toggle chart section
function toggleSection(canvasId, show) {
    var canvas = document.getElementById(canvasId);
    if (canvas) {
        var section = canvas.closest('.ups-rpt-chart-section');
        if (section) section.style.display = show ? '' : 'none';
    }
}


// render all charts
function upsRptRenderAll(data) {
    if (typeof upsRptRenderDcpi === 'function') upsRptRenderDcpi(data);

    var catFilter = document.getElementById('upsRptCategoryFilter');
    var cat = catFilter ? catFilter.value : 'ALL';

    if (cat === 'KOMPARASI') {
        upsRptRenderKomparasi(data);
        return;
    }

    if (!data) return;

    var t = data.trends || {};
    var phaseByDevice = ((data.meta || {}).device_phase_types) || {};

    function hasValidData(trend) {
        if (!trend || !trend.devices) return false;
        return trend.devices.some(function (d) {
            return d.values.some(function (v) { return v !== null && v !== undefined && v > 0; });
        });
    }

    var outputPhaseSTrend = filterTrendDevices(t.voltage_out_b, function (d) {
        return phaseByDevice[d.id] === '3/3';
    });
    var outputPhaseTTrend = filterTrendDevices(t.voltage_out_c, function (d) {
        return phaseByDevice[d.id] === '3/3';
    });

    var hasInS = hasValidData(t.voltage_in_b);
    var hasInT = hasValidData(t.voltage_in_c);
    var hasOutS = hasValidData(outputPhaseSTrend);
    var hasOutT = hasValidData(outputPhaseTTrend);

    // --- Comparison bar ---
    buildComparisonChart('chartComparison', data.comparison || {});

    // --- Input (1/1 Combined Average) ---
    var avgInput = avgTrend(t.voltage_in_a, t.voltage_in_b, t.voltage_in_c);
    t.voltage_in_avg = avgInput;
    buildLineChart('chartInputAvg', avgInput, 'V', [
        { value: 210, color: 'rgba(239,68,68,0.6)', label: 'Crit 210V' },
        { value: 217, color: 'rgba(245,158,11,0.6)', label: 'Warn 217V' },
        { value: 223, color: 'rgba(245,158,11,0.6)', label: 'Warn 223V' },
        { value: 230, color: 'rgba(239,68,68,0.6)', label: 'Crit 230V' },
    ]);
    buildLineChart('chartInputFreq', t.input_frequency, 'Hz', [
        { value: 47, color: 'rgba(245,158,11,0.5)', label: 'Min 47Hz' },
        { value: 53, color: 'rgba(245,158,11,0.5)', label: 'Max 53Hz' },
    ]);

    // --- Output (1/1 Combined Average) ---
    var avgOutput = avgTrend(t.voltage_out_a, t.voltage_out_b, t.voltage_out_c);
    t.voltage_out_avg = avgOutput;
    buildLineChart('chartOutputAvg', avgOutput, 'V', [
        { value: 215, color: 'rgba(239,68,68,0.6)', label: 'Crit 215V' },
        { value: 217, color: 'rgba(245,158,11,0.6)', label: 'Warn 217V' },
        { value: 223, color: 'rgba(245,158,11,0.6)', label: 'Warn 223V' },
        { value: 225, color: 'rgba(239,68,68,0.6)', label: 'Crit 225V' },
    ]);
    buildLineChart('chartOutputFreq', t.output_frequency, 'Hz', [
        { value: 47, color: 'rgba(245,158,11,0.5)', label: 'Min 47Hz' },
        { value: 53, color: 'rgba(245,158,11,0.5)', label: 'Max 53Hz' },
    ]);

    // --- Ground ---
    buildLineChart('chartGround', t.grounding_resistance, 'Î©', [
        { value: 3, color: 'rgba(245,158,11,0.55)', label: 'Warn 3Î©' },
        { value: 5, color: 'rgba(239,68,68,0.55)', label: 'Crit 5Î©' },
    ]);

    // --- Battery ---
    buildLineChart('chartBatVoltage', t.battery_voltage, 'V', []);
    buildLineChart('chartBatPercent', t.battery_percent, '%', [
        { value: 50, color: 'rgba(245,158,11,0.55)', label: 'Warn 50%' },
        { value: 20, color: 'rgba(239,68,68,0.55)', label: 'Crit 20%' },
    ]);

    // --- Temperature ---
    buildLineChart('chartTempMachine', t.ambient_temp, '°C', [
        { value: 30, color: 'rgba(245,158,11,0.55)', label: 'Warn 30°C' },
        { value: 35, color: 'rgba(239,68,68,0.55)', label: 'Crit 35°C' },
    ]);
    buildLineChart('chartTempRoom', t.room_temp, '°C', [
        { value: 30, color: 'rgba(245,158,11,0.55)', label: 'Warn 30°C' },
        { value: 35, color: 'rgba(239,68,68,0.55)', label: 'Crit 35°C' },
    ]);

    // --- Load ---
    buildLineChart('chartLoad', t.load_percent, '%', [
        { value: 80, color: 'rgba(245,158,11,0.55)', label: 'Warn 80%' },
        { value: 90, color: 'rgba(239,68,68,0.55)', label: 'Crit 90%' },
    ]);
}

// stat chips update
function upsRptUpdateStats(stats) {
    if (!stats) return;
    function fmt(val, unit) {
        return (val !== null && val !== undefined) ? (val + ' <small>' + unit + '</small>') : '<span style="font-size:0.75rem; color:#94a3b8; font-weight:500; letter-spacing: 0;">Data tidak tersedia</span>';
    }
    function set(id, val, hasData) {
        var el = document.getElementById(id);
        if (el) {
            el.innerHTML = val;
            if (hasData === false) {
                if (el.closest('.ups-rpt-stat-chip')) el.closest('.ups-rpt-stat-chip').style.display = 'none';
            } else {
                if (el.closest('.ups-rpt-stat-chip')) el.closest('.ups-rpt-stat-chip').style.display = '';
            }
        }
    }

    function setStatusClass(chipId, val, warnThresh, critThresh, invert) {
        var chip = document.getElementById(chipId);
        if (!chip) return;

        chip.classList.remove('ups-rpt-stat-chip--warn', 'ups-rpt-stat-chip--crit');
        if (val === null || val === undefined) return;

        if (invert) {
            if (val <= critThresh) chip.classList.add('ups-rpt-stat-chip--crit');
            else if (val <= warnThresh) chip.classList.add('ups-rpt-stat-chip--warn');
        } else {
            if (val >= critThresh) chip.classList.add('ups-rpt-stat-chip--crit');
            else if (val >= warnThresh) chip.classList.add('ups-rpt-stat-chip--warn');
        }
    }

    function setStatusRangeClass(chipId, val, warnLow, warnHigh, critLow, critHigh) {
        var chip = document.getElementById(chipId);
        if (!chip) return;

        chip.classList.remove('ups-rpt-stat-chip--warn', 'ups-rpt-stat-chip--crit');
        if (val === null || val === undefined || val <= 0) return;

        if (val < critLow || val > critHigh) {
            chip.classList.add('ups-rpt-stat-chip--crit');
        } else if (val < warnLow || val > warnHigh) {
            chip.classList.add('ups-rpt-stat-chip--warn');
        }
    }

    var vinAvgVal = stats.avg_vin != null ? stats.avg_vin : stats.avg_vin_a;
    var voutAvgVal = stats.avg_vout != null ? stats.avg_vout : stats.avg_vout_a;

    set('statVinAvg', fmt(vinAvgVal, 'V'));
    set('statFreqIn', fmt(stats.avg_freq_in, 'Hz'));
    set('statVoutAvg', fmt(voutAvgVal, 'V'));
    set('statFreqOut', fmt(stats.avg_freq_out, 'Hz'));

    setStatusRangeClass('chipVinAvg', vinAvgVal, 217, 223, 210, 230);
    setStatusRangeClass('chipVoutAvg', voutAvgVal, 217, 223, 215, 225);
    set('statGroundAvg', fmt(stats.avg_ground, 'Î©'));
    set('statGroundMin', fmt(stats.min_ground, 'Ω'));
    set('statGroundMax', fmt(stats.max_ground, 'Ω'));
    set('statBatV', fmt(stats.avg_bat_v, 'V'));
    set('statBatPct', fmt(stats.avg_bat_pct, '%'));
    set('statBatPctMin', fmt(stats.min_bat_pct, '%'));

    set('statTempAvg', fmt(stats.avg_temp, '°C'));
    set('statTempMax', fmt(stats.max_temp, '°C'));
    setStatusClass('chipTempMax', stats.max_temp, 30, 35, false);

    set('statRoomTempAvg', fmt(stats.avg_room_temp, '°C'));
    set('statRoomTempMax', fmt(stats.max_room_temp, '°C'));
    setStatusClass('chipRoomTempMax', stats.max_room_temp, 30, 35, false);

    set('statLoadAvg', fmt(stats.avg_load, '%'));

    setStatusClass('chipBatPctMin', stats.min_bat_pct, 50, 20, true);
}

// filter
var _currentResolution = window.UPS_RPT_RESOLUTION || 'daily';

function upsRptSetResolution(res, btn) {
    if (!res && btn) {
        res = btn.dataset.res || (btn.id === 'btnResWeekly' ? 'weekly' : (btn.id === 'btnResMonthly' ? 'monthly' : 'daily'));
    }
    if (!res || res === 'undefined') res = 'daily';
    _currentResolution = res;
    
    document.querySelectorAll('.ups-rpt-res-btn').forEach(function (b) {
        if (btn) b.classList.toggle('is-active', b === btn);
        else b.classList.toggle('is-active', (b.dataset.res || b.id.replace('btnRes','').toLowerCase()) === res);
    });

    var groupFrom = document.getElementById('upsRptDateFromGroup');
    var labelTo = document.getElementById('upsRptDateToLabel');
    var timeGroup = document.getElementById('upsRptTimeGroup');
    var timeGroup2 = document.getElementById('upsRptTimeGroup2');
    var aggGroup = document.getElementById('upsRptAggregationGroup');

    if (groupFrom && labelTo) {
        if (res === 'daily') {
            groupFrom.style.display = 'none';
            labelTo.textContent = 'Pilih Tanggal';
            if (timeGroup) timeGroup.style.display = '';
            if (timeGroup2) timeGroup2.style.display = '';
            if (aggGroup) aggGroup.style.display = '';
        } else {
            groupFrom.style.display = '';
            labelTo.textContent = 'Sampai Tanggal';
            if (timeGroup) timeGroup.style.display = 'none';
            if (timeGroup2) timeGroup2.style.display = 'none';
            if (aggGroup) aggGroup.style.display = 'none';

            // Auto adjust dateFrom based on resolution (7 days for weekly, 30 days for monthly)
            var dateFromEl = document.getElementById('upsRptDateFrom');
            var dateToEl = document.getElementById('upsRptDateTo');
            if (dateFromEl && dateToEl && dateToEl.value) {
                var daysToSubtract = res === 'monthly' ? 29 : 6;
                var d = new Date(dateToEl.value);
                d.setDate(d.getDate() - daysToSubtract);
                var yyyy = d.getFullYear();
                var mm = String(d.getMonth() + 1).padStart(2, '0');
                var dd = String(d.getDate()).padStart(2, '0');
                dateFromEl.value = yyyy + '-' + mm + '-' + dd;
            }
        }
    }
}

function upsRptFilterChange() {
    var siteId = (document.getElementById('upsRptSiteFilter') || {}).value;
    var cat = (document.getElementById('upsRptCategoryFilter') || {}).value;
    var btnDaily = document.getElementById('btnResDaily');

    var resToggle = document.getElementById('upsRptResolutionToggle');
    var dateFromGroup = document.getElementById('upsRptDateFromGroup');
    var dateToGroup = document.getElementById('upsRptDateToGroup');
    var timeGroup = document.getElementById('upsRptTimeGroup');
    var timeGroup2 = document.getElementById('upsRptTimeGroup2');
    var aggGroup = document.getElementById('upsRptAggregationGroup');
    var monthGroup = document.getElementById('upsRptMonthGroup');

    if (cat === 'KOMPARASI') {
        if (resToggle) resToggle.parentElement.style.display = 'none';
        if (dateFromGroup) dateFromGroup.style.display = 'none';
        if (dateToGroup) dateToGroup.style.display = 'none';
        if (timeGroup) timeGroup.style.display = 'none';
        if (timeGroup2) timeGroup2.style.display = 'none';
        if (aggGroup) aggGroup.style.display = 'none';
        if (monthGroup) monthGroup.style.display = '';
    } else {
        if (resToggle) resToggle.parentElement.style.display = '';
        if (monthGroup) monthGroup.style.display = 'none';

        // Restore standard visibility based on resolution
        if (typeof _currentResolution !== 'undefined' && _currentResolution === 'daily') {
            if (dateFromGroup) dateFromGroup.style.display = 'none';
            if (dateToGroup) dateToGroup.style.display = '';
            var labelTo = document.getElementById('upsRptDateToLabel');
            if (labelTo) labelTo.textContent = 'Pilih Tanggal';
            if (timeGroup) timeGroup.style.display = '';
            if (timeGroup2) timeGroup2.style.display = '';
            if (aggGroup) aggGroup.style.display = '';
        } else {
            if (dateFromGroup) dateFromGroup.style.display = '';
            if (dateToGroup) dateToGroup.style.display = '';
            var labelTo = document.getElementById('upsRptDateToLabel');
            if (labelTo) labelTo.textContent = 'Sampai Tanggal';
            if (timeGroup) timeGroup.style.display = 'none';
            if (timeGroup2) timeGroup2.style.display = 'none';
            if (aggGroup) aggGroup.style.display = 'none';
        }

        if (siteId === 'ALL') {
            if (btnDaily) btnDaily.style.display = 'none';
            if (typeof _currentResolution !== 'undefined' && _currentResolution === 'daily') {
                var btnWeekly = document.getElementById('btnResWeekly');
                if (btnWeekly && typeof upsRptSetResolution === 'function') {
                    upsRptSetResolution('weekly', btnWeekly);
                }
            }
        } else {
            if (btnDaily) btnDaily.style.display = '';
        }
    }
}

function upsRptApply() {
    var siteId = (document.getElementById('upsRptSiteFilter') || {}).value || '';
    var dateFrom = (document.getElementById('upsRptDateFrom') || {}).value || '';
    var dateTo = (document.getElementById('upsRptDateTo') || {}).value || '';
    var fromH = (document.getElementById('upsRptTimeFromH') || {}).value || '00';
    var fromM = (document.getElementById('upsRptTimeFromM') || {}).value || '00';
    var toH = (document.getElementById('upsRptTimeToH') || {}).value || '23';
    var toM = (document.getElementById('upsRptTimeToM') || {}).value || '59';
    var timeFrom = fromH + ':' + fromM;
    var timeTo = toH + ':' + toM;
    var agg = (document.getElementById('upsRptAggregationFilter') || {}).value || 'average';
    var cat = (document.getElementById('upsRptCategoryFilter') || {}).value || 'ALL';
    var res = _currentResolution;

    if (cat === 'KOMPARASI') {
        var monthVal = (document.getElementById('upsRptMonth') || {}).value;
        if (monthVal) {
            var y = parseInt(monthVal.split('-')[0], 10);
            var m = parseInt(monthVal.split('-')[1], 10);
            var lastDay = new Date(y, m, 0).getDate();
            dateFrom = monthVal + '-01';
            dateTo = monthVal + '-' + String(lastDay).padStart(2, '0');
            res = 'monthly';
        }
    }

    if (!siteId) { alert('Pilih site terlebih dahulu.'); return; }

    var url = new URL(window.location.href);
    url.searchParams.set('site_id', siteId);
    url.searchParams.set('date_from', dateFrom);
    url.searchParams.set('date_to', dateTo);
    url.searchParams.set('time_from', timeFrom);
    url.searchParams.set('time_to', timeTo);
    url.searchParams.set('aggregation', agg);
    url.searchParams.set('ups_category', cat);
    url.searchParams.set('resolution', res);

    var loading = document.getElementById('upsRptLoading');
    if (loading) loading.style.display = 'flex';
    var panels = document.querySelectorAll('.ups-rpt-panel, .ups-rpt-chart-card, .ups-rpt-stat-card, .ups-rpt-summary-card');
    panels.forEach(function (el) {
        el.classList.add('ups-shimmering');
    });

    window.location.href = url.toString();
}

// AJAX refresh (no full page reload)
function upsRptAjaxReload() {
    var siteId = (document.getElementById('upsRptSiteFilter') || {}).value || '';
    var dateFrom = (document.getElementById('upsRptDateFrom') || {}).value || '';
    var dateTo = (document.getElementById('upsRptDateTo') || {}).value || '';
    var fromH = (document.getElementById('upsRptTimeFromH') || {}).value || '00';
    var fromM = (document.getElementById('upsRptTimeFromM') || {}).value || '00';
    var toH = (document.getElementById('upsRptTimeToH') || {}).value || '23';
    var toM = (document.getElementById('upsRptTimeToM') || {}).value || '59';
    var timeFrom = fromH + ':' + fromM;
    var timeTo = toH + ':' + toM;
    var agg = (document.getElementById('upsRptAggregationFilter') || {}).value || 'average';
    var cat = (document.getElementById('upsRptCategoryFilter') || {}).value || 'ALL';
    var res = _currentResolution;

    if (cat === 'KOMPARASI') {
        var monthVal = (document.getElementById('upsRptMonth') || {}).value;
        if (monthVal) {
            var y = parseInt(monthVal.split('-')[0], 10);
            var m = parseInt(monthVal.split('-')[1], 10);
            var lastDay = new Date(y, m, 0).getDate();
            dateFrom = monthVal + '-01';
            dateTo = monthVal + '-' + String(lastDay).padStart(2, '0');
            res = 'monthly';
        }
    }

    if (!siteId) return Promise.resolve();

    var panels = document.querySelectorAll('.ups-rpt-panel, .ups-rpt-chart-card, .ups-rpt-stat-card, .ups-rpt-summary-card');
    panels.forEach(function (el) {
        el.classList.add('ups-shimmering');
    });

    var loading = document.getElementById('upsRptLoading');
    if (loading) loading.style.display = 'flex';

    var params = new URLSearchParams({
        site_id: siteId,
        date_from: dateFrom,
        date_to: dateTo,
        time_from: timeFrom,
        time_to: timeTo,
        aggregation: agg,
        ups_category: cat,
        resolution: res
    });
    return fetch(window.UPS_REPORT_API_URL + '?' + params, {
        headers: { 'X-CSRF-TOKEN': window.CSRF_TOKEN, 'Accept': 'application/json' },
    })
        .then(function (r) { return r.json(); })
        .then(function (json) {
            if (json.status === 'success') {
                upsRptRenderAll(json.data);
                upsRptUpdateStats(json.data.stats);
                document.querySelectorAll('.ups-rpt-period-text').forEach(function (el) {
                    el.textContent = (json.data.meta || {}).period_label || '';
                });
                var del = document.getElementById('upsRptDeviceCount');
                var totalDevices = (json.data.meta || {}).total_devices || 0;
                if (del) del.textContent = totalDevices;

                var empty = document.getElementById('upsReportEmptyState');
                var content = document.getElementById('upsReportContent');
                var komparasi = document.getElementById('upsReportKomparasi');

                if (totalDevices > 0) {
                    if (empty) empty.style.display = 'none';
                    if (cat === 'KOMPARASI') {
                        if (content) content.style.display = 'none';
                        if (komparasi) komparasi.style.display = 'flex';
                    } else {
                        if (content) content.style.display = 'flex';
                        if (komparasi) komparasi.style.display = 'none';
                    }
                } else {
                    if (empty) empty.style.display = '';
                    if (content) content.style.display = 'none';
                    if (komparasi) komparasi.style.display = 'none';
                }
            }
        })
        .catch(function (e) { console.error('[UpsReport] reload error:', e); })
        .finally(function () {
            if (loading) loading.style.display = 'none';
            panels.forEach(function (el) {
                el.classList.remove('ups-shimmering');
            });
        });
}

// init
document.addEventListener('DOMContentLoaded', function () {
    var data = window.UPS_REPORT_DATA;
    var totalDevices = (data && data.meta) ? (data.meta.total_devices || 0) : 0;

    if (data && data.trends) {
        upsRptRenderAll(data);
        upsRptUpdateStats(data.stats);
    }

    var del = document.getElementById('upsRptDeviceCount');
    if (del) del.textContent = totalDevices;

    if (data && data.meta) {
        document.querySelectorAll('.ups-rpt-period-text').forEach(function (el) {
            el.textContent = data.meta.period_label || '';
        });
    }

    var empty = document.getElementById('upsReportEmptyState');
    var content = document.getElementById('upsReportContent');
    var komparasi = document.getElementById('upsReportKomparasi');
    var cat = (document.getElementById('upsRptCategoryFilter') || {}).value || 'ALL';

    if (window.UPS_RPT_SITE_ID) {
        if (totalDevices > 0) {
            if (empty) empty.style.display = 'none';
            if (cat === 'KOMPARASI') {
                if (content) content.style.display = 'none';
                if (komparasi) komparasi.style.display = 'flex';
            } else {
                if (content) content.style.display = 'flex';
                if (komparasi) komparasi.style.display = 'none';
            }
        } else {
            if (empty) empty.style.display = '';
            if (content) content.style.display = 'none';
            if (komparasi) komparasi.style.display = 'none';
        }
    }

    // Sync resolution buttons
    var res = window.UPS_RPT_RESOLUTION || 'daily';

    upsRptFilterChange();

    document.querySelectorAll('.ups-rpt-res-btn').forEach(function (btn) {
        btn.addEventListener('click', function (e) {
            e.preventDefault();
            upsRptSetResolution(this.dataset.res, this);
        });
        if (btn.textContent.trim().toLowerCase() === res) {
            btn.classList.add('is-active');
        }
    });

    // Tab Navigation Logic
    document.querySelectorAll('.ups-rpt-tabs').forEach(function (tabContainer) {
        var btns = tabContainer.querySelectorAll('.ups-rpt-tab-btn');
        btns.forEach(function (btn) {
            btn.addEventListener('click', function () {
                var targetId = 'tab-' + this.getAttribute('data-target');

                // Remove active from all siblings
                btns.forEach(function (b) { b.classList.remove('active'); });
                this.classList.add('active');

                // Find the associated content wrapper (next sibling usually)
                var contentWrapper = tabContainer.nextElementSibling;
                if (contentWrapper && contentWrapper.classList.contains('ups-rpt-tab-content')) {
                    // Hide all sections
                    var sections = contentWrapper.querySelectorAll('.ups-rpt-chart-section');
                    sections.forEach(function (sec) { sec.classList.remove('active'); });

                    // Show target section
                    var targetSec = contentWrapper.querySelector('#' + targetId);
                    if (targetSec) {
                        targetSec.classList.add('active');
                        // Force Chart.js to resize since it was hidden
                        setTimeout(function () {
                            var canvases = targetSec.querySelectorAll('canvas');
                            canvases.forEach(function (canvas) {
                                var chart = _charts[canvas.id];
                                if (chart) {
                                    chart.resize();
                                    chart.update('none');
                                }
                            });
                        }, 50);
                    }
                }
            });
        });
    });

});

function upsRptRenderKomparasi(data) {
    const isL = (localStorage.getItem('ups-theme') || 'dark') === 'light';
    const textColor = isL ? '#64748b' : '#8b9ab7';
    const gridColor = isL ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.07)';
    const tooltipBg = isL ? 'rgba(255,255,255,0.98)' : 'rgba(8,11,24,0.96)';
    const rowHover = isL ? 'rgba(241,245,249,0.7)' : 'rgba(30,41,59,0.35)';
    const rowBorder = isL ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.08)';

    const t = data.trends || {};
    const labels = (t.voltage_in_a && t.voltage_in_a.labels) ? t.voltage_in_a.labels : [];
    if (!labels || labels.length === 0) return;

    function getVals(metric, devName) {
        if (!t[metric] || !t[metric].devices) return labels.map(function () { return null; });
        var dev = null;
        for (var i = 0; i < t[metric].devices.length; i++) {
            if (t[metric].devices[i].name === devName) { dev = t[metric].devices[i]; break; }
        }
        return dev ? dev.values : labels.map(function () { return null; });
    }

    function fmtVal(v) { return (v !== null && v !== undefined) ? v : '-'; }

    function loadColor(v) {
        if (v === null || v === undefined || v === '-') return '';
        if (v >= 90) return 'color:#ef4444; font-weight:700;';
        if (v >= 80) return 'color:#f59e0b; font-weight:700;';
        return 'color:#4ade80;';
    }

    // ===== 1. LOAD & POWER TABLE =====
    var loadDC = getVals('load_percent', 'UPS DC');
    var loadUSER = getVals('load_percent', 'UPS USER');
    var wattDC = getVals('total_watts', 'UPS DC');
    var wattUSER = getVals('total_watts', 'UPS USER');

    var tbody = document.getElementById('tableKomparasiLoadBody');
    if (tbody) {
        var html = '';
        for (var i = 0; i < labels.length; i++) {
            var ldc = fmtVal(loadDC[i]);
            var lu = fmtVal(loadUSER[i]);
            var wdc = fmtVal(wattDC[i]);
            var wu = fmtVal(wattUSER[i]);

            html += '<tr style="border-bottom:1px solid ' + rowBorder + '; transition:background 0.2s;" ' +
                'onmouseenter="this.style.background=\'' + rowHover + '\'" ' +
                'onmouseleave="this.style.background=\'transparent\'">' +
                '<td style="padding:12px 20px; font-weight:600; letter-spacing:0.3px;">' + labels[i] + '</td>' +
                '<td style="padding:12px 16px; text-align:center; font-variant-numeric:tabular-nums; ' + loadColor(ldc) + '">' + (ldc !== '-' ? ldc + '%' : '-') + '</td>' +
                '<td style="padding:12px 16px; text-align:center; font-variant-numeric:tabular-nums; ' + loadColor(lu) + '">' + (lu !== '-' ? lu + '%' : '-') + '</td>' +
                '<td style="padding:12px 16px; text-align:center; font-variant-numeric:tabular-nums; color:var(--ups-muted);">' + (wdc !== '-' ? wdc + ' W' : '-') + '</td>' +
                '<td style="padding:12px 16px; text-align:center; font-variant-numeric:tabular-nums; color:var(--ups-muted);">' + (wu !== '-' ? wu + ' W' : '-') + '</td>' +
                '</tr>';
        }
        tbody.innerHTML = html;
    }

    // ===== 2. BAR CHARTS: Voltage, Temperature =====
    function buildKompBar(canvasId, metricId, unit, thresholds) {
        destroyChart(canvasId);
        var canvas = document.getElementById(canvasId);
        if (!canvas) return;

        var valDC = getVals(metricId, 'UPS DC');
        var valUSER = getVals(metricId, 'UPS USER');

        var datasets = [
            {
                label: 'UPS DC',
                data: valDC,
                backgroundColor: 'rgba(96,165,250,0.85)',
                hoverBackgroundColor: '#60a5fa',
                borderRadius: 5,
                borderSkipped: false,
                barPercentage: 0.7,
                categoryPercentage: 0.65
            },
            {
                label: 'UPS USER',
                data: valUSER,
                backgroundColor: 'rgba(74,222,128,0.85)',
                hoverBackgroundColor: '#4ade80',
                borderRadius: 5,
                borderSkipped: false,
                barPercentage: 0.7,
                categoryPercentage: 0.65
            }
        ];

        if (thresholds) {
            for (var ti = 0; ti < thresholds.length; ti++) {
                var th = thresholds[ti];
                datasets.push({
                    type: 'line',
                    label: th.label,
                    data: labels.map(function () { return th.value; }),
                    borderColor: th.color,
                    borderWidth: 1.5,
                    borderDash: [6, 4],
                    pointRadius: 0,
                    pointHoverRadius: 0,
                    fill: false,
                    order: -1
                });
            }
        }

        _charts[canvasId] = new Chart(canvas, {
            type: 'bar',
            data: { labels: labels, datasets: datasets },
            options: {
                layout: { padding: { top: 55, left: 10, right: 10, bottom: 10 } },
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: {
                        display: false  // Legend is in the header badge
                    },
                    datalabels: {
                        display: true,
                        rotation: -90,
                        anchor: 'end',
                        align: 'end',
                        offset: 4,
                        clip: false,
                        font: { size: 9, weight: '700' },
                        color: function (ctx) {
                            if (ctx.datasetIndex >= 2) return 'transparent';
                            var v = ctx.dataset.data[ctx.dataIndex];
                            if (v === null || v === undefined) return 'transparent';

                            if (thresholds && thresholds.length > 0) {
                                var warn = null, crit = null;
                                for (var i = 0; i < thresholds.length; i++) {
                                    var lbl = thresholds[i].label.toLowerCase();
                                    if (lbl.indexOf('warn') >= 0) warn = thresholds[i].value;
                                    if (lbl.indexOf('crit') >= 0 || lbl.indexOf('max') >= 0) crit = thresholds[i].value;
                                }
                                if (crit !== null && v >= crit) return '#ef4444';
                                if (warn !== null && v >= warn) return '#f59e0b';
                            }
                            return ctx.datasetIndex === 0 ? '#60a5fa' : '#4ade80';
                        },
                        formatter: function (val) {
                            return val !== null ? val : '';
                        }
                    },
                    tooltip: {
                        backgroundColor: tooltipBg,
                        titleColor: isL ? '#0f172a' : '#eef4ff',
                        bodyColor: textColor,
                        borderColor: isL ? 'rgba(148,163,184,0.25)' : 'rgba(148,163,184,0.18)',
                        borderWidth: 1,
                        padding: 12,
                        callbacks: {
                            label: function (ctx) {
                                if (ctx.datasetIndex >= 2) return null;
                                var v = ctx.parsed.y;
                                return ' ' + ctx.dataset.label + ': ' + (v !== null ? v + ' ' + unit : '\u2014');
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { color: textColor, font: { size: 11, weight: '600' }, maxRotation: 45, minRotation: 0 },
                        border: { display: false }
                    },
                    y: {
                        grid: { color: gridColor, drawTicks: false },
                        ticks: { color: textColor, font: { size: 10 }, padding: 8 },
                        grace: '15%',
                        border: { display: false }
                    }
                }
            }
        });
    }

    // ===== 3. HORIZONTAL BAR: Battery =====
    function buildKompHorizBar(canvasId, metricId, unit) {
        destroyChart(canvasId);
        var canvas = document.getElementById(canvasId);
        if (!canvas) return;

        // Adjust parent height based on site count
        var wrap = document.getElementById('chartKompBatteryWrap');
        if (wrap) {
            var h = Math.max(250, labels.length * 50 + 60);
            wrap.style.height = h + 'px';
        }

        var valDC = getVals(metricId, 'UPS DC');
        var valUSER = getVals(metricId, 'UPS USER');

        _charts[canvasId] = new Chart(canvas, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'UPS DC',
                        data: valDC,
                        backgroundColor: 'rgba(96,165,250,0.85)',
                        hoverBackgroundColor: '#60a5fa',
                        borderRadius: 5,
                        barPercentage: 0.6,
                        categoryPercentage: 0.7
                    },
                    {
                        label: 'UPS USER',
                        data: valUSER,
                        backgroundColor: 'rgba(74,222,128,0.85)',
                        hoverBackgroundColor: '#4ade80',
                        borderRadius: 5,
                        barPercentage: 0.6,
                        categoryPercentage: 0.7
                    }
                ]
            },
            options: {
                indexAxis: 'y',
                layout: { padding: { right: 60, left: 10, top: 10, bottom: 10 } },
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { display: false },
                    datalabels: {
                        display: true,
                        anchor: 'end',
                        align: 'end',
                        offset: 6,
                        clip: false,
                        font: { size: 10, weight: '700' },
                        color: function (ctx) {
                            var v = ctx.dataset.data[ctx.dataIndex];
                            if (v === null || v === undefined) return 'transparent';

                            if (v <= 20) return '#ef4444';
                            if (v <= 50) return '#f59e0b';

                            return ctx.datasetIndex === 0 ? '#60a5fa' : '#4ade80';
                        },
                        formatter: function (val) {
                            return val !== null ? val + '%' : '';
                        }
                    },
                    tooltip: {
                        backgroundColor: tooltipBg,
                        titleColor: isL ? '#0f172a' : '#eef4ff',
                        bodyColor: textColor,
                        borderColor: isL ? 'rgba(148,163,184,0.25)' : 'rgba(148,163,184,0.18)',
                        borderWidth: 1,
                        padding: 12,
                        callbacks: {
                            label: function (ctx) {
                                return ' ' + ctx.dataset.label + ': ' + (ctx.parsed.x !== null ? ctx.parsed.x + ' ' + unit : '\u2014');
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { color: gridColor, drawTicks: false },
                        ticks: { color: textColor, font: { size: 10 }, padding: 8, callback: function (v) { return v + '%'; } },
                        max: 115,
                        border: { display: false }
                    },
                    y: {
                        grid: { display: false },
                        ticks: { color: textColor, font: { size: 11, weight: '600' }, padding: 6 },
                        border: { display: false }
                    }
                }
            }
        });
    }

    // ===== 4. MASTER SUMMARY TABLE =====
    var masterHead = document.getElementById('tableKomparasiMasterHead');
    var masterBody = document.getElementById('tableKomparasiMasterBody');
    if (masterHead && masterBody) {
        var cols = [
            { key: 'load_percent', label: 'Load %', unit: '%' },
            { key: 'total_watts', label: 'Daya (W)', unit: 'W' },
            { key: 'voltage_in_avg', label: 'V In (Avg)', unit: 'V' },
            { key: 'voltage_out_avg', label: 'V Out (Avg)', unit: 'V' },
            { key: 'input_frequency', label: 'Freq In', unit: 'Hz' },
            { key: 'output_frequency', label: 'Freq Out', unit: 'Hz' },
            { key: 'grounding_resistance', label: 'Ground', unit: '\u03a9' },
            { key: 'battery_voltage', label: 'Bat V', unit: 'V' },
            { key: 'battery_percent', label: 'Bat %', unit: '%' },
            { key: 'battery_runtime_min', label: 'Bat Min', unit: 'min' },
            { key: 'ambient_temp', label: 'T. Mesin', unit: '\u00b0C' },
            { key: 'room_temp', label: 'T. Room', unit: '\u00b0C' }
        ];

        var hStyle = 'padding:12px 14px; text-align:center; font-weight:700; color:var(--ups-muted); font-size:10px; letter-spacing:0.5px; text-transform:uppercase;';
        var headHtml = '<th style="' + hStyle + ' text-align:left; min-width:70px;">Site</th>' +
            '<th style="' + hStyle + ' min-width:50px;">Cat</th>';
        for (var c = 0; c < cols.length; c++) {
            headHtml += '<th style="' + hStyle + ' min-width:65px;">' + cols[c].label + '</th>';
        }
        masterHead.innerHTML = headHtml;

        var bHtml = '';
        for (var si = 0; si < labels.length; si++) {
            var cats = ['UPS DC', 'UPS USER'];
            for (var ci = 0; ci < cats.length; ci++) {
                var catName = cats[ci];
                var catColor = catName === 'UPS DC' ? '#60a5fa' : '#4ade80';
                var catLabel = catName === 'UPS DC' ? 'DC' : 'USER';
                var isFirstRow = ci === 0;

                bHtml += '<tr style="border-bottom:1px solid ' + rowBorder + '; transition:background 0.2s;" ' +
                    'onmouseenter="this.style.background=\'' + rowHover + '\'" ' +
                    'onmouseleave="this.style.background=\'transparent\'">';

                if (isFirstRow) {
                    bHtml += '<td rowspan="2" style="padding:10px 14px; font-weight:700; font-size:12px; border-right:1px solid ' + rowBorder + ';">' + labels[si] + '</td>';
                }

                bHtml += '<td style="padding:8px 10px; text-align:center;">' +
                    '<span style="display:inline-block; padding:2px 8px; border-radius:6px; font-size:10px; font-weight:700; color:' + catColor + '; background:' + catColor + '18; letter-spacing:0.3px;">' + catLabel + '</span>' +
                    '</td>';

                for (var k = 0; k < cols.length; k++) {
                    var vals = getVals(cols[k].key, catName);
                    var v = fmtVal(vals[si]);
                    var cellStyle = 'padding:8px 14px; text-align:center; font-variant-numeric:tabular-nums; font-size:12px;';

                    // Color coding for specific metrics
                    if (v !== '-') {
                        if (cols[k].key === 'ambient_temp' || cols[k].key === 'room_temp') {
                            if (v >= 35) cellStyle += ' color:#ef4444; font-weight:700;';
                            else if (v >= 30) cellStyle += ' color:#f59e0b; font-weight:600;';
                            else cellStyle += ' color:var(--ups-text);';
                        } else if (cols[k].key === 'battery_percent') {
                            if (v <= 20) cellStyle += ' color:#ef4444; font-weight:700;';
                            else if (v <= 50) cellStyle += ' color:#f59e0b; font-weight:600;';
                            else cellStyle += ' color:var(--ups-text);';
                        } else if (cols[k].key === 'grounding_resistance') {
                            if (v >= 5) cellStyle += ' color:#ef4444; font-weight:700;';
                            else if (v >= 3) cellStyle += ' color:#f59e0b; font-weight:600;';
                            else cellStyle += ' color:var(--ups-text);';
                        } else if (cols[k].key === 'load_percent') {
                            if (v >= 90) cellStyle += ' color:#ef4444; font-weight:700;';
                            else if (v >= 80) cellStyle += ' color:#f59e0b; font-weight:600;';
                            else cellStyle += ' color:var(--ups-text);';
                        } else {
                            cellStyle += ' color:var(--ups-text);';
                        }
                    } else {
                        cellStyle += ' color:var(--ups-muted);';
                    }

                    bHtml += '<td style="' + cellStyle + '">' + (v !== '-' ? v : '\u2014') + '</td>';
                }
                bHtml += '</tr>';
            }
        }
        masterBody.innerHTML = bHtml;
    }

    // ===== RENDER CHARTS =====
    buildKompBar('chartKompVin', 'voltage_in_avg', 'V', [
        { value: 198, color: 'rgba(245,158,11,0.5)', label: 'Min Warn' },
        { value: 242, color: 'rgba(239,68,68,0.5)', label: 'Max' }
    ]);

    buildKompBar('chartKompVout', 'voltage_out_avg', 'V', [
        { value: 198, color: 'rgba(245,158,11,0.5)', label: 'Min Warn' },
        { value: 242, color: 'rgba(239,68,68,0.5)', label: 'Max' }
    ]);

    buildKompBar('chartKompTempAmbient', 'ambient_temp', '\u00b0C', [
        { value: 30, color: 'rgba(245,158,11,0.5)', label: 'Warn' },
        { value: 35, color: 'rgba(239,68,68,0.5)', label: 'Crit' }
    ]);

    buildKompBar('chartKompTempRoom', 'room_temp', '\u00b0C', [
        { value: 30, color: 'rgba(245,158,11,0.5)', label: 'Warn' },
        { value: 35, color: 'rgba(239,68,68,0.5)', label: 'Crit' }
    ]);

    buildKompHorizBar('chartKompBattery', 'battery_percent', '%');
}

// ===== EXPORT KOMPARASI TO EXCEL (CUSTOM STYLE) =====
function upsRptExportKomparasiCustom() {
    if (typeof XLSX === 'undefined') {
        alert('Gagal mengekspor data. XLSX library tidak tersedia.');
        return;
    }
    var data = window.UPS_REPORT_DATA;
    if (!data || !data.trends) return;

    var t = data.trends;
    var labels = (t.voltage_in_a && t.voltage_in_a.labels) ? t.voltage_in_a.labels : [];
    if (labels.length === 0) return;

    var metrics = [
        { key: 'load_percent', label: 'Load (%)' },
        { key: 'total_watts', label: 'Daya (W)' },
        { key: 'voltage_in_avg', label: 'Tegangan In (V)' },
        { key: 'voltage_out_avg', label: 'Tegangan Out (V)' },
        { key: 'input_frequency', label: 'Freq In (Hz)' },
        { key: 'output_frequency', label: 'Freq Out (Hz)' },
        { key: 'grounding_resistance', label: 'Ground (\u03a9)' },
        { key: 'battery_voltage', label: 'Bat (V)' },
        { key: 'battery_percent', label: 'Bat (%)' },
        { key: 'battery_runtime_min', label: 'Bat (Min)' },
        { key: 'ambient_temp', label: 'T. Mesin (\u00b0C)' },
        { key: 'room_temp', label: 'T. Room (\u00b0C)' }
    ];

    function getVals(metric, devName) {
        if (!t[metric] || !t[metric].devices) return labels.map(function () { return null; });
        var dev = t[metric].devices.find(function (d) { return d.name === devName; });
        return (dev && dev.values) ? dev.values : labels.map(function () { return null; });
    }

    var wsData = [];
    var row1 = ["site"];
    var row2 = [""];

    // Build Headers
    for (var i = 0; i < metrics.length; i++) {
        row1.push(metrics[i].label);
        row1.push(""); // empty for merge
        row2.push("dc");
        row2.push("user");
    }
    wsData.push(row1);
    wsData.push(row2);

    // Build Data Rows
    for (var i = 0; i < labels.length; i++) {
        var row = [labels[i]];
        for (var m = 0; m < metrics.length; m++) {
            var dcVals = getVals(metrics[m].key, 'UPS DC');
            var userVals = getVals(metrics[m].key, 'UPS USER');

            var dcV = dcVals[i] !== null && dcVals[i] !== undefined ? dcVals[i] : "-";
            var usV = userVals[i] !== null && userVals[i] !== undefined ? userVals[i] : "-";

            row.push(dcV);
            row.push(usV);
        }
        wsData.push(row);
    }

    // Create Worksheet
    var ws = XLSX.utils.aoa_to_sheet(wsData);

    // Merges
    if (!ws['!merges']) ws['!merges'] = [];
    ws['!merges'].push({ s: { r: 0, c: 0 }, e: { r: 1, c: 0 } }); // site

    for (var m = 0; m < metrics.length; m++) {
        var startCol = 1 + (m * 2);
        ws['!merges'].push({ s: { r: 0, c: startCol }, e: { r: 0, c: startCol + 1 } });
    }

    // Add styles
    var borderAll = {
        top: { style: "thin" },
        bottom: { style: "thin" },
        left: { style: "thin" },
        right: { style: "thin" }
    };

    var headerStyle = {
        font: { bold: true },
        alignment: { horizontal: "center", vertical: "center" },
        border: borderAll
    };

    var dataStyle = {
        alignment: { horizontal: "center", vertical: "center" },
        border: borderAll
    };

    for (var R = 0; R < wsData.length; R++) {
        for (var C = 0; C < wsData[R].length; C++) {
            var cellRef = XLSX.utils.encode_cell({ c: C, r: R });
            if (!ws[cellRef]) continue;

            if (R === 0 || R === 1) {
                ws[cellRef].s = headerStyle;
            } else {
                ws[cellRef].s = dataStyle;
            }
        }
    }

    var wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, "Komparasi");

    var monthVal = (document.getElementById('upsRptMonth') || {}).value || 'Report';
    var filename = 'Komparasi_UPS_' + monthVal + '.xlsx';

    XLSX.writeFile(wb, filename);
}

