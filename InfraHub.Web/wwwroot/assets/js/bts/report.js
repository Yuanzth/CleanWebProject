let currentResolution = 'daily';

let filterDebounceTimer = null;
let isFetching = false;

const CHART_REGISTRY = {};


const _towerColorCache = {};

function towerColor(index) {
    if (_towerColorCache[index] !== undefined) return _towerColorCache[index];

    // Golden ratio konjugat — menjamin distribusi hue yang paling seragam
    const GOLDEN_RATIO = 0.618033988749895;

    // Zona hue yang diizinkan: 65°–330° (menghindari merah & kuning)
    const HUE_MIN   = 65;
    const HUE_RANGE = 265; 

    // Setiap index melangkah sebesar golden ratio dalam ruang [0,1)
    const h   = (index * GOLDEN_RATIO) % 1.0;
    const hue = Math.round(HUE_MIN + h * HUE_RANGE);

    // Variasi ringan pada saturation & lightness antar warna agar lebih beragam secara visual
    const saturation = 65 + (index % 4) * 5;   
    const lightness  = 52 + (index % 3) * 6;   

    const color = `hsl(${hue}, ${saturation}%, ${lightness}%)`;
    _towerColorCache[index] = color;
    return color;
}
const CHART_DEFAULTS = {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    onHover: function(e, activeElements, chart) {
        const hovered = chart.getElementsAtEventForMode(e, 'nearest', { intersect: true }, false);
        
        if (hovered && hovered.length > 0) {
            const index = hovered[0].datasetIndex;
            chart.data.datasets.forEach((ds, i) => {
                if (!ds._origBorderColor) ds._origBorderColor = ds.borderColor;
                if (i === index) {
                    ds.borderColor = ds._origBorderColor;
                    ds.borderWidth = 3;
                } else if (ds.label && !ds.label.startsWith('__thr_')) {
                    ds.borderColor = 'rgba(148, 163, 184, 0.15)'; // dim color
                    ds.borderWidth = 1;
                }
            });
            chart.update();
            chart._faded = true;
        } else if (chart._faded) {
            // Restore jika tidak hover elemen spesifik apa pun
            chart.data.datasets.forEach((ds) => {
                if (ds._origBorderColor) {
                    ds.borderColor = ds._origBorderColor;
                    ds.borderWidth = 2;
                }
            });
            chart.update();
            chart._faded = false;
        }
    },
    animation: { duration: 400 },
    plugins: {
        legend: {
            display: true,
            position: 'bottom',
            onHover: function(e, legendItem, legend) {
                const index = legendItem.datasetIndex;
                const chart = legend.chart;
                chart.data.datasets.forEach((ds, i) => {
                    if (!ds._origBorderColor) ds._origBorderColor = ds.borderColor;
                    if (i === index) {
                        ds.borderColor = ds._origBorderColor;
                        ds.borderWidth = 3;
                    } else if (ds.label && !ds.label.startsWith('__thr_')) {
                        // Dim others
                        ds.borderColor = 'rgba(148, 163, 184, 0.15)';
                        ds.borderWidth = 1;
                    }
                });
                chart.update();
            },
            onLeave: function(e, legendItem, legend) {
                const chart = legend.chart;
                chart.data.datasets.forEach((ds) => {
                    if (ds._origBorderColor) {
                        ds.borderColor = ds._origBorderColor;
                        ds.borderWidth = 2;
                    }
                });
                chart.update();
            },
            labels: {
                color: '#94a3b8',
                boxWidth: 10,
                font: { size: 10 },
                padding: 8,
                usePointStyle: true,
                pointStyleWidth: 10,
            },
        },
        tooltip: {
            backgroundColor: '#1e2535',
            titleColor: '#94a3b8',
            bodyColor: '#f1f5f9',
            borderColor: '#334155',
            borderWidth: 1,
            padding: 10,
        },
    },
    scales: {
        x: {
            grid: { color: 'rgba(148,163,184,0.08)' },
            ticks: { color: '#64748b', font: { size: 11 } },
        },
        y: {
            grid: { color: 'rgba(148,163,184,0.08)' },
            ticks: { color: '#64748b', font: { size: 11 } },
            beginAtZero: false,
        },
    },
};



//CHART DEFAULTS
function hourlyTickCallback(value) {
    const label = this.getLabelForValue(value);
    // Tampilkan label jam loncat tiap 3 jam 
    const m = label && label.match(/^(\d{2}):00$/);
    if (m) return parseInt(m[1], 10) % 3 === 0 ? label : '';
    return label;
}

function computeYScale(devices, options) {
    const allVals = (devices || [])
        .flatMap(d => d.values || [])
        .filter(v => v != null && isFinite(v));

    if (allVals.length === 0) {
        return {
            yMin: undefined,
            yMax: undefined,
            visThresholds: options.thresholds || [],
        };
    }

    const rawMin = Math.min(...allVals);
    const rawMax = Math.max(...allVals);
    
    // Minimal range so flat data has a fallback scale height
    let valRange = rawMax - rawMin;
    if (valRange < 0.1) valRange = 1.0;

    // Strict padding: 20% of the actual data range above and below
    const padTop    = valRange * 0.2;
    const padBottom = valRange * 0.2;

    let yMax = rawMax + padTop;
    let yMin = rawMin - padBottom;

    if (options.minCeiling !== undefined && yMax < options.minCeiling) {
        yMax = options.minCeiling;
    }

    if (options.maxCeiling !== undefined && yMax > options.maxCeiling) {
        yMax = options.maxCeiling;
    }
    if (options.beginAtZero) {
        yMin = 0;
    }

    const visThresholds = (options.thresholds || []).filter(t => {
        return t.value >= yMin && t.value <= yMax;
    });

    return { yMin, yMax, visThresholds };
}

//Helper untuk menentukan apakah suatu nilai melanggar threshold (mengembalikan warna merah/kuning)
function getAlertColorForValue(value, thresholds, defaultColor) {
    if (!thresholds || thresholds.length === 0 || value == null) return defaultColor;
    let color = defaultColor;
    
    thresholds.forEach(t => {
        const lbl = (t.label || '').toLowerCase();
        let isViolated = false;
        
        const isLowerLimit = lbl.includes('<') || lbl.includes('≤') || lbl.includes('lower') || lbl.includes('fuel') || lbl.includes('lux');
        
        if (isLowerLimit) {
            if (value <= t.value) isViolated = true;
        } else {
            if (value >= t.value) isViolated = true;
        }
        
        if (isViolated) {
            color = t.color.replace(/0\.\d+\)/, '1)'); 
        }
    });
    return color;
}


function buildChartDatasets(devices, visThresholds, labels, allThresholds = [], heightMap = {}, perDeviceThresholds = {}) {
    return [
        ...devices.map((dev, i) => {
            const baseBorderColor = towerColor(i);
            // Buat warna transparan yang kompatibel dengan format hsl() maupun rgb()
            const baseBgColor = baseBorderColor.startsWith('hsl(')
                ? baseBorderColor.replace('hsl(', 'hsla(').replace(')', ', 0.06)')
                : baseBorderColor.replace('rgb(', 'rgba(').replace(')', ', 0.06)');

            // Tambahkan tinggi tower ke legend
            const heightM = heightMap[dev.name];
            const labelWithHeight = heightM ? `${dev.name} (${heightM}m)` : dev.name;

            // Ambil threshold spesifik untuk device ini (per-device), atau fallback ke global
            const devThr = perDeviceThresholds[dev.name] || null;

            function getDeviceAlertColor(value, fallback) {
                if (value == null) return fallback;
                if (devThr) {
                    if (value >= devThr.critCm) return '#ef4444';
                    if (value >= devThr.warnCm) return '#f59e0b';
                    return fallback;
                }
                return getAlertColorForValue(value, allThresholds, fallback);
            }

            return {
                label: labelWithHeight,
                data:  dev.values,
                borderColor: baseBorderColor,
                backgroundColor: baseBgColor,
                borderWidth: 2,

                pointBackgroundColor: function(context) {
                    if (context.type !== 'data') return baseBorderColor;
                    return getDeviceAlertColor(context.raw, baseBorderColor);
                },
                pointBorderColor: function(context) {
                    if (context.type !== 'data') return baseBorderColor;
                    return getDeviceAlertColor(context.raw, baseBorderColor);
                },
                pointRadius: function(context) {
                    if (context.type !== 'data') return 0;
                    const alerted = devThr
                        ? (context.raw >= devThr.warnCm)
                        : !!getAlertColorForValue(context.raw, allThresholds, null);
                    if (alerted) return 3;
                    return labels.length > 30 ? 0 : 2.5;
                },

                pointHoverRadius: 7,
                tension: 0.35,
                fill: false,
                spanGaps: true,
            };
        }),
        ...visThresholds.map(t => ({
            label: `__thr_${t.label}`,
            data:  labels.map(() => t.value),
            borderColor: t.color,
            borderWidth: 1.5,
            borderDash: t.dash || [5, 4],
            pointRadius: 0,
            fill: false,
            tension: 0,
            spanGaps: true,
            order: 99,
        }))
    ];
}


function initMultiLineChart(canvasId, trendData, options = {}) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return null;

    if (CHART_REGISTRY[canvasId]) {
        CHART_REGISTRY[canvasId].destroy();
        delete CHART_REGISTRY[canvasId];
    }

    const labels     = trendData?.labels  || [];
    const devices    = trendData?.devices || [];
    const unit       = options.unit       || '';
    const isDailyX   = labels.length > 0 && /^\d{2}:00$/.test(labels[0]);

    // Compute dynamic Y bounds from actual data
    const { yMin, yMax, visThresholds } = computeYScale(devices, options);

    // Kompilasi datasets garis perangkat dan garis peringatan filter
    const datasets = buildChartDatasets(
        devices, visThresholds, labels,
        options.thresholds || [],
        options.heightMap || {},
        options.perDeviceThresholds || {}
    );

    const chart = new Chart(ctx, {
        type: 'line',
        data: { labels, datasets },
        options: {
            ...CHART_DEFAULTS,
            plugins: {
                ...CHART_DEFAULTS.plugins,
                legend: {
                    ...CHART_DEFAULTS.plugins.legend,
                    display: devices.length > 0,
                    labels: {
                        ...CHART_DEFAULTS.plugins.legend.labels,
                        filter: item => !item.text.startsWith('__thr_'),
                        generateLabels: function(chart) {
                            const defaults = Chart.defaults.plugins.legend.labels.generateLabels(chart);
                            return defaults
                                .filter(item => !item.text.startsWith('__thr_'))
                                .map(item => {
                                    const ds = chart.data.datasets[item.datasetIndex];
                                    if (ds && typeof ds.borderColor === 'string') {
                                        item.fillStyle   = ds.backgroundColor;
                                        item.strokeStyle = ds.borderColor;
                                        item.lineWidth   = 2;
                                        item.lineDash    = [];
                                    }
                                    return item;
                                });
                        },
                    },
                },
                tooltip: {
                    ...CHART_DEFAULTS.plugins.tooltip,
                    callbacks: {
                        label: ctx => {
                            if (ctx.dataset.label.startsWith('__thr_')) return null;
                            const v = ctx.parsed.y;
                            return ` ${ctx.dataset.label}: ${v != null ? v : '—'}${unit ? ' ' + unit : ''}`;
                        },
                    },
                },
            },
            scales: {
                ...CHART_DEFAULTS.scales,
                x: {
                    ...CHART_DEFAULTS.scales.x,
                    ticks: {
                        ...CHART_DEFAULTS.scales.x.ticks,
                        callback: isDailyX ? hourlyTickCallback : undefined,
                    },
                },
                y: {
                    ...CHART_DEFAULTS.scales.y,
                    beginAtZero: options.beginAtZero || false, // Let Chart.js handle 0-floor if requested
                    suggestedMin: yMin,
                    suggestedMax: yMax,
                    title: options.yLabel
                        ? { display: true, text: options.yLabel, color: '#64748b', font: { size: 11 } }
                        : { display: false },
                },
            },
        },
    });

    CHART_REGISTRY[canvasId] = chart;
    return chart;
}

// CHART DEFINITIONS — 8 parameter charts
function getChartDefinitions(d) {
    const thr  = d?.thresholds || {};
    const pd   = d?.trends_per_device || {};
    const towerHeights = d?.tower?.tower_heights || {};

    return [
        // Power
        {
            id: 'chartVoltageTrend',
            data: pd.voltage,
            options: {
                yLabel: 'V', unit: 'V',
                thresholds: [
                    { value: 225, color: '#f59e0b', label: `Upper Warn ≥225V`, dash: [5,4] },
                    { value: 215, color: '#f59e0b', label: `Lower Warn ≤215V`, dash: [5,4] },
                    { value: 230, color: '#ef4444', label: `Upper Crit ≥230V`, dash: [3,3] },
                    { value: 210, color: '#ef4444', label: `Lower Crit ≤210V`, dash: [3,3] },
                ],
            },
        },
        {
            id: 'chartFuelLevel',
            data: pd.fuel_level,
            options: {
                yLabel: '%', unit: '%',
                beginAtZero: true,
                maxCeiling: 100,
                thresholds: [
                    { value: thr.fuel_warning  || 35, color: '#f59e0b', label: `Warn ≤${thr.fuel_warning || 35}%`, dash: [5,4] },
                    { value: thr.fuel_critical || 25, color: '#ef4444', label: `Crit ≤${thr.fuel_critical || 25}%`, dash: [3,3] },
                ],
            },
        },

        // Environmental
        {
            id: 'chartWindTrend',
            data: pd.wind_speed,
            options: {
                yLabel: 'km/h', unit: 'km/h',
                beginAtZero: true,
                minCeiling: 5,
                thresholds: [
                    { value: thr.wind_warning  || 35, color: '#f59e0b',  label: `Warn ${thr.wind_warning || 35} km/h`,  dash: [5,4] },
                    { value: thr.wind_critical || 80, color: '#ef4444',  label: `Crit ${thr.wind_critical || 80} km/h`, dash: [3,3] },
                ],
            },
        },
        {
            id: 'chartRainfall',
            data: pd.rainfall,
            options: {
                yLabel: 'mm/h', unit: 'mm/h',
                beginAtZero: true,
                minCeiling: 5,
                thresholds: [
                    { value: thr.rainfall_warn     || 30, color: '#f59e0b',  label: `Warn ${thr.rainfall_warn || 30} mm/h`,     dash: [5,4] },
                    { value: thr.rainfall_critical || 50, color: '#ef4444',  label: `Crit ${thr.rainfall_critical || 50} mm/h`, dash: [3,3] },
                ],
            },
        },
        {
            id: 'chartLuminosity',
            data: pd.luminosity,
            options: {
                yLabel: 'lux', unit: 'lux',
                beginAtZero: true,
                thresholds: [
                    { value: thr.luminosity_warn     || 900, color: '#f59e0b',  label: `Warn ≤${thr.luminosity_warn || 900} lux`,     dash: [5,4] },
                    { value: thr.luminosity_critical || 750, color: '#ef4444',  label: `Crit ≤${thr.luminosity_critical || 750} lux`, dash: [3,3] },
                ],
            },
        },
        {
            id: 'chartDustLevel',
            data: pd.dust_level,
            options: {
                yLabel: 'μg/m³', unit: 'μg/m³',
                beginAtZero: true,
                thresholds: [
                    { value: thr.dust_warn     || 35, color: '#f59e0b',  label: `Warn >${thr.dust_warn || 35} μg/m³`,     dash: [5,4] },
                    { value: thr.dust_critical || 75, color: '#ef4444',  label: `Crit >${thr.dust_critical || 75} μg/m³`, dash: [3,3] },
                ],
            },
        },
        {
            id: 'chartTemperature',
            data: pd.temperature,
            options: {
                yLabel: '°C', unit: '°C',
                beginAtZero: false,
                thresholds: [
                    { value: thr.temperature_warn     || 38, color: '#f59e0b',  label: `Warn >${thr.temperature_warn || 38}°C`,     dash: [5,4] },
                    { value: thr.temperature_critical || 55, color: '#ef4444',  label: `Crit >${thr.temperature_critical || 55}°C`, dash: [3,3] },
                ],
            },
        },

        // Tower
        {
            id: 'chartTilt',
            data: pd.tilt,
            options: (() => {
                // Hitung threshold cm per device: Warning = 0.5%, Critical = 1.0% dari tinggi (meter × 100 = cm)
                const perDeviceThresholds = {};
                Object.entries(towerHeights).forEach(([zone, hm]) => {
                    perDeviceThresholds[zone] = {
                        warnCm: hm * 100 * 0.005,
                        critCm: hm * 100 * 0.010,
                    };
                });
                return {
                    yLabel: 'cm', unit: 'cm',
                    beginAtZero: true,
                    minCeiling: 10,
                    heightMap: towerHeights,
                    perDeviceThresholds,
                    thresholds: [],
                };
            })(),
        },
        {
            id: 'chartGroundResistance',
            data: pd.ground_resistance,
            options: {
                yLabel: 'Ω', unit: 'Ω',
                beginAtZero: true,
                thresholds: [
                    { value: thr.ground_warn     || 3.5, color: '#f59e0b',  label: `Warn ${thr.ground_warn || 3.5}Ω`,     dash: [5,4] },
                    { value: thr.ground_critical || 5.0, color: '#ef4444',  label: `Crit ${thr.ground_critical || 5.0}Ω`, dash: [3,3] },
                ],
            },
        },
        {
            id: 'chartLightningCurrent',
            data: pd.lightning_current,
            options: {
                yLabel: 'kA', unit: 'kA',
                beginAtZero: true,
                thresholds: [
                    { value: thr.lightning_critical || 20, color: '#ef4444', label: `Crit ${thr.lightning_critical || 20} kA`, dash: [3,3] },
                ],
            },
        },
    ];
}

// INIT & UPDATE ALL CHARTS
function initAllCharts() {
    const d = window.REPORT_DATA;
    if (!d) return;
    getChartDefinitions(d).forEach(({ id, data, options }) => {
        initMultiLineChart(id, data, options);
    });
}

function updateAllCharts(d) {
    if (!d) return;
    getChartDefinitions(d).forEach(({ id, data, options }) => {
        const chart = CHART_REGISTRY[id];
        if (!chart) { initMultiLineChart(id, data, options); return; }

        const labels    = data?.labels  || [];
        const devices   = data?.devices || [];
        const isDailyX  = labels.length > 0 && /^\d{2}:00$/.test(labels[0]);

        // Recompute dynamic Y scale from fresh data
        const { yMin, yMax, visThresholds } = computeYScale(devices, options);

        chart.data.labels   = labels;
        chart.data.datasets = buildChartDatasets(
            devices, visThresholds, labels,
            options.thresholds || [],
            options.heightMap || {},
            options.perDeviceThresholds || {}
        );
        chart.options.plugins.legend.display = devices.length > 0;
        chart.options.scales.x.ticks.callback = isDailyX ? hourlyTickCallback : undefined;
        chart.options.scales.y.suggestedMin = yMin;
        chart.options.scales.y.suggestedMax = yMax;
        delete chart.options.scales.y.min;
        delete chart.options.scales.y.max;
        chart.update('active');
    });
}

// DOM UPDATE — UPDATE SEMUA PANEL

function setText(id, value) {
    const el = document.getElementById(id);
    if (el) el.textContent = value ?? '';
}

function setClass(el, prefix, cls) {
    if (!el) return;
    [...el.classList].filter(c => c.startsWith(prefix)).forEach(c => el.classList.remove(c));
    el.classList.add(`${prefix}${cls}`);
}

function updateKpiBar(kpi) {
    const keys = ['overall_health', 'power', 'weather', 'tower', 'incidents'];
    keys.forEach(key => {
        const k = kpi[key];
        if (!k) return;
        const sev = k.status || 'ok';

        const valEl = document.getElementById(`kpiVal-${key}`);
        if (valEl) valEl.textContent = k.value ?? '';

        setText(`kpiSub-${key}`, k.sub || '');

        const badgeEl = document.getElementById(`kpiBadge-${key}`);
        if (badgeEl) {
            badgeEl.textContent = k.badge || '';
            setClass(badgeEl, 'rpt-badge--', sev);
        }

        const cardEl = document.getElementById(`kpiCard-${key}`);
        if (cardEl) {
            setClass(cardEl, 'rpt-kpi-card--', sev);
            const iconEl = cardEl.querySelector('.rpt-kpi-icon');
            if (iconEl) {
                ['ok','warn','crit','blue','slate'].forEach(c => iconEl.classList.remove(`rpt-kpi-icon--${c}`));
                iconEl.classList.add(`rpt-kpi-icon--${sev === 'crit' ? 'crit' : sev === 'warn' ? 'warn' : 'ok'}`);
            }
        }
    });
}

function updatePowerPanel(power, meta) {
    setText('rptPowerPeriod', meta?.period_label || '');
    updateChartResolutionLabel(currentResolution, 'rptPowerChartLabel');

    // Stat bar: MIN VOLTAGE + MAX VOLTAGE
    const vMin = power.min_voltage;
    const vMax = power.max_voltage;
    setText('statVoltMin', vMin != null ? vMin : '—');
    setText('statVoltMax', vMax != null ? vMax : '—');

    // Colour chips vs threshold
    const lo = power.trend_lower_limit || 215;
    const hi = power.trend_upper_limit || 225;
    const minChip = document.getElementById('statVoltMin')?.closest('.rpt-stat-chip');
    if (minChip) setClass(minChip, 'rpt-stat-chip--', vMin == null ? 'blue' : vMin >= lo ? 'ok' : 'crit');
    const maxChip = document.getElementById('statVoltMax')?.closest('.rpt-stat-chip');
    if (maxChip) setClass(maxChip, 'rpt-stat-chip--', vMax == null ? 'blue' : vMax <= hi ? 'ok' : 'warn');
}

function updateIncidentPanel(incidents) {
    setText('rptIncidentTotal', incidents?.total);

    const critEl = document.getElementById('rptTotalCrit');
    if (critEl) critEl.innerHTML = `<i class="fa-solid fa-circle-exclamation"></i> ${incidents.total_critical} Critical`;

    const warnEl = document.getElementById('rptTotalWarn');
    if (warnEl) warnEl.innerHTML = `<i class="fa-solid fa-triangle-exclamation"></i> ${incidents.total_warning} Warning`;

    const container = document.getElementById('rptIncidentBars');
    if (!container || !incidents?.by_category) return;
    const maxTotal = Math.max(...incidents.by_category.map(c => c.total), 1);
    container.innerHTML = incidents.by_category.map(cat => {
        const cp = Math.round((cat.critical || 0) / maxTotal * 100);
        const wp = Math.round((cat.warning  || 0) / maxTotal * 100);
        return `<div class="rpt-inc-bar-row" data-category="${cat.category}" data-critical="${cat.critical||0}" data-warning="${cat.warning||0}" onmouseenter="showIncTooltip(event,this)" onmouseleave="hideIncTooltip()">
            <div class="rpt-inc-bar-label">${cat.category}</div>
            <div class="rpt-inc-bar-track">
                <div class="rpt-inc-bar-fill rpt-inc-bar-fill--crit" style="width:${cp}%"></div>
                <div class="rpt-inc-bar-fill rpt-inc-bar-fill--warn" style="width:${wp}%"></div>
            </div>
            <div class="rpt-inc-bar-count">${cat.total}</div>
        </div>`;
    }).join('');
}

function updateTowerPanel(tower) {
    // Stat bar chips — MIN + MAX (avg only in global KPI bar)
    setText('statTiltMin',     tower?.min_tilt   != null ? tower.min_tilt   : '—');
    setText('statTiltMax',     tower?.max_tilt);
    setText('statGroundMin',   tower?.min_ground_resistance != null ? tower.min_ground_resistance : '—');
    setText('statGroundMax',   tower?.max_ground_resistance);
    setText('statLightningStrikes', tower?.total_spd_events != null ? tower.total_spd_events : '—');
    setText('statLightningkA',      tower?.max_lightning_current != null ? tower.max_lightning_current : '—');

    // Colour max tilt chip
    const tiltMaxChip = document.getElementById('statTiltMax')?.closest('.rpt-stat-chip');
    if (tiltMaxChip && tower) {
        const m = tower.max_tilt;
        setClass(tiltMaxChip, 'rpt-stat-chip--', m < 1.0 ? 'ok' : m < 2.0 ? 'warn' : 'crit');
    }
    // Colour lightning chip
    const lcChip = document.getElementById('statLightningkA')?.closest('.rpt-stat-chip');
    if (lcChip && tower) {
        setClass(lcChip, 'rpt-stat-chip--', tower.max_lightning_current < 20 ? 'ok' : 'crit');
    }

    // Attention list
    const attentionList = document.querySelector('.rpt-attention-list');
    if (!attentionList || !tower?.attention_towers) return;
    const heading = attentionList.querySelector('.rpt-attention-list__heading')?.outerHTML || '';
    const items = tower.attention_towers.length > 0
        ? tower.attention_towers.map(t => `
            <div class="rpt-attention-item">
                <div class="rpt-attention-item__row">
                    <span class="rpt-attention-item__code">${t.site_code}</span>
                    <span class="rpt-attention-item__tilt">${t.tilt}°</span>
                </div>
                <div class="rpt-attention-item__note">${t.note}</div>
            </div>`).join('')
        : `<div class="rpt-attention-empty"><i class="fa-solid fa-circle-check"></i> Semua tower dalam batas aman</div>`;
    attentionList.innerHTML = heading + items;

    updateChartResolutionLabel(currentResolution, 'rptTowerChartLabel');
}

function updateWeatherPanel(weather, meta) {
    setText('rptWeatherPeriod', meta?.period_label || '');

    // Stat bar chips — MIN + MAX (avg only in global KPI bar)
    setText('statWindMin',        weather?.min_wind_speed  != null ? weather.min_wind_speed  : '—');
    setText('statWindMax',        weather?.max_wind_speed);
    setText('statRainMin',        weather?.min_rainfall    != null ? weather.min_rainfall    : '—');
    setText('statRainMax',        weather?.max_rainfall);
    // Temperature chips
    setText('statTempMin',        weather?.min_temperature != null ? weather.min_temperature : '—');
    setText('statTempMax',        weather?.max_temperature != null ? weather.max_temperature : '—');

    // Colour wind max chip
    const windMaxChip = document.getElementById('statWindMax')?.closest('.rpt-stat-chip');
    if (windMaxChip && weather) {
        setClass(windMaxChip, 'rpt-stat-chip--', weather.wind_ok ? 'ok' : 'crit');
    }
    // Colour rain max chip
    const rainMaxChip = document.getElementById('statRainMax')?.closest('.rpt-stat-chip');
    if (rainMaxChip && weather) {
        setClass(rainMaxChip, 'rpt-stat-chip--', weather.rainfall_ok ? 'ok' : 'warn');
    }
    // Colour temperature max chip
    const tempMaxChip = document.getElementById('statTempMax')?.closest('.rpt-stat-chip');
    if (tempMaxChip && weather?.max_temperature != null) {
        const t = weather.max_temperature;
        setClass(tempMaxChip, 'rpt-stat-chip--', t < 38 ? 'ok' : t < 55 ? 'warn' : 'crit');
    }

    const weatherOk = weather?.wind_ok && weather?.rainfall_ok && weather?.lightning_ok;
    const badge = document.getElementById('rptWeatherStatus');
    if (badge) {
        setClass(badge, 'rpt-sev--', weatherOk ? 'info' : 'crit');
        badge.innerHTML = weatherOk
            ? '<i class="fa-solid fa-circle-check"></i> All Normal'
            : '<i class="fa-solid fa-circle-exclamation"></i> Alert';
    }

    updateChartResolutionLabel(currentResolution, 'rptWeatherChartLabel');
}

function updateAllPanels(data) {
    if (!data) return;
    const { meta, kpi, power, incidents, tower, weather } = data;

    setText('rptTotalSites',   meta?.total_sites);
    setText('rptTotalDevices', meta?.total_devices);

    if (kpi)       updateKpiBar(kpi);
    if (power)     updatePowerPanel(power, meta);
    if (incidents) updateIncidentPanel(incidents);
    if (tower)     updateTowerPanel(tower);
    if (weather)   updateWeatherPanel(weather, meta);

    updateAllCharts(data);
}

// RESOLUTION LABELS

function updateChartResolutionLabel(resolution, elementId) {
    if (resolution === 'daily') {
        const toVal = document.getElementById('rptDateTo')?.value || '';
        let label = 'Today';
        if (toVal) {
            const d = new Date(toVal + 'T00:00:00');
            label = d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
        }
        setText(elementId, label);
    } else {
        setText(elementId, resolution === 'weekly' ? 'Last 7 Days' : 'Last 30 Days');
    }
}

// RESOLUTION TOGGLES

function setGlobalResolution(resolution, _btnEl) {
    currentResolution = resolution;

    // Sync active state on ALL three panel toggles
    ['powerResolutionToggle', 'weatherResolutionToggle', 'towerResolutionToggle'].forEach(id => {
        const toggle = document.getElementById(id);
        if (!toggle) return;
        toggle.querySelectorAll('.rpt-res-btn').forEach(btn => {
            const btnRes = btn.textContent.trim().toLowerCase();
            btn.classList.toggle('rpt-res-btn--active', btnRes === resolution);
        });
    });

    // Update chart date labels in all panels
    ['rptPowerChartLabel', 'rptWeatherChartLabel', 'rptTowerChartLabel'].forEach(id => {
        updateChartResolutionLabel(resolution, id);
    });

    fetchAndUpdate();
}

/* ─────────────────────────────────────────────────────────────────────────
   AJAX FETCH
   ───────────────────────────────────────────────────────────────────────── */
function getFilterParams() {
    const area = document.getElementById('rptAreaFilter')?.value || '';
    const from = document.getElementById('rptDateFrom')?.value   || '';
    const to   = document.getElementById('rptDateTo')?.value     || '';
    const p = new URLSearchParams();
    if (area) p.set('area_id', area);
    if (from) p.set('date_from', from);
    if (to)   p.set('date_to', to);
    // Unified global resolution — same value sent for all panels
    p.set('power_resolution',   currentResolution);
    p.set('weather_resolution', currentResolution);
    p.set('tower_resolution',   currentResolution);
    return p;
}

function setLoadingState(on) {
    isFetching = on;
    const btn = document.querySelector('.rpt-btn-apply');
    if (btn) {
        btn.disabled = on;
        btn.innerHTML = on
            ? '<i class="fa-solid fa-spinner fa-spin"></i> Loading...'
            : '<i class="fa-solid fa-rotate"></i> Terapkan';
    }
    document.querySelectorAll('.rpt-chart-wrap').forEach(el => {
        el.style.opacity = on ? '0.45' : '1';
        el.style.transition = 'opacity 0.2s';
    });
}

function reportFilterChange() {
    clearTimeout(filterDebounceTimer);
    filterDebounceTimer = setTimeout(() => {
        const siteId = document.getElementById('rptAreaFilter')?.value || '';
        if (!siteId) {
            showErrorToast('Pilih site terlebih dahulu sebelum melihat report.');
            return;
        }

        // If panels are not rendered yet (first load without site), do full-page load.
        if (!document.getElementById('panelPower')) {
            window.location.search = getFilterParams().toString();
            return;
        }

        fetchAndUpdate();
    }, 350);
}

async function fetchAndUpdate() {
    if (isFetching) return;
    const apiUrl = window.REPORT_API_URL;
    if (!apiUrl) { console.warn('[Report] REPORT_API_URL undefined'); return; }

    const siteId = document.getElementById('rptAreaFilter')?.value || '';
    if (!siteId) {
        showErrorToast('Pilih site terlebih dahulu sebelum melihat report.');
        return;
    }

    setLoadingState(true);
    try {
        const res = await fetch(`${apiUrl}?${getFilterParams()}`, {
            headers: { 'Accept': 'application/json', 'X-CSRF-TOKEN': window.CSRF_TOKEN || '' },
        });
        if (!res.ok) { showErrorToast(`Server error: ${res.status}`); return; }
        const json = await res.json();
        if (json.status !== 'success' || !json.data) { showErrorToast('Invalid server response'); return; }

        window.REPORT_DATA = json.data;
        updateAllPanels(json.data);
    } catch (e) {
        console.error('[Report] Fetch error:', e);
        showErrorToast('Connection failed. Make sure server is running.');
    } finally {
        setLoadingState(false);
    }
}

/* ─────────────────────────────────────────────────────────────────────────
   INCIDENT TOOLTIP
   ───────────────────────────────────────────────────────────────────────── */
function showIncTooltip(event, rowEl) {
    const tooltip = document.getElementById('rptIncTooltip');
    if (!tooltip) return;
    setText('rptIncTtCategory', rowEl.dataset.category || '');
    setText('rptIncTtCrit', rowEl.dataset.critical || '0');
    setText('rptIncTtWarn', rowEl.dataset.warning  || '0');
    tooltip.style.display = 'block';
    const ttW = tooltip.offsetWidth || 160, ttH = tooltip.offsetHeight || 80;
    const off = 14, vw = window.innerWidth, vh = window.innerHeight;
    let left = event.clientX + off, top = event.clientY + off;
    if (left + ttW > vw - 8) left = event.clientX - ttW - off;
    if (top  + ttH > vh - 8) top  = event.clientY - ttH - off;
    tooltip.style.left = `${left}px`;
    tooltip.style.top  = `${top}px`;
}

function hideIncTooltip() {
    const t = document.getElementById('rptIncTooltip');
    if (t) t.style.display = 'none';
}

/* ─────────────────────────────────────────────────────────────────────────
   ERROR TOAST
   ───────────────────────────────────────────────────────────────────────── */
function showErrorToast(message) {
    let toast = document.getElementById('rptErrorToast');
    if (!toast) {
        toast = document.createElement('div');
        toast.id = 'rptErrorToast';
        Object.assign(toast.style, {
            position:'fixed', bottom:'24px', right:'24px', zIndex:'9999',
            background:'#ef4444', color:'#fff', padding:'12px 20px',
            borderRadius:'8px', fontSize:'13px', maxWidth:'320px',
            boxShadow:'0 4px 12px rgba(0,0,0,.3)',
            display:'none', opacity:'0', transition:'opacity .3s',
        });
        document.body.appendChild(toast);
    }
    toast.textContent = message;
    toast.style.display = 'block';
    requestAnimationFrame(() => { toast.style.opacity = '1'; });
    setTimeout(() => {
        toast.style.opacity = '0';
        setTimeout(() => { toast.style.display = 'none'; }, 300);
    }, 4000);
}

/* ─────────────────────────────────────────────────────────────────────────
   INIT
   ───────────────────────────────────────────────────────────────────────── */
document.addEventListener('DOMContentLoaded', () => {
    initAllCharts();

    // Init all resolution labels with the global default ('daily')
    ['rptPowerChartLabel', 'rptWeatherChartLabel', 'rptTowerChartLabel'].forEach(id => {
        updateChartResolutionLabel(currentResolution, id);
    });

    document.addEventListener('scroll', hideIncTooltip, { passive: true });
    document.addEventListener('click', e => {
        if (!e.target.closest('.rpt-inc-bar-row')) hideIncTooltip();
    });
});