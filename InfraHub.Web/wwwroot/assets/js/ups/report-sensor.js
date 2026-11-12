// COLOR PALETTE
const UPS_RPT_COLORS = [
    { border: '#60a5fa', bg: 'rgba(96,165,250,0.10)' },
    { border: '#4ade80', bg: 'rgba(74,222,128,0.10)' },
    { border: '#fb923c', bg: 'rgba(251,146,60,0.10)' },
    { border: '#a78bfa', bg: 'rgba(167,139,250,0.10)' },
    { border: '#f472b6', bg: 'rgba(244,114,182,0.10)' },
];

function upsRptColor(idx) { return UPS_RPT_COLORS[idx % UPS_RPT_COLORS.length]; }

const _charts = {};

if (typeof ChartDataLabels !== 'undefined') {
    Chart.register(ChartDataLabels);
    Chart.defaults.plugins.datalabels.display = false;
}

function destroyChart(id) {
    if (_charts[id]) { _charts[id].destroy(); delete _charts[id]; }
}

const initialTheme = localStorage.getItem('ups-theme') || 'dark';
Chart.defaults.font.family = "'Inter', 'Segoe UI', system-ui, sans-serif";
Chart.defaults.color = initialTheme === 'light' ? '#64748b' : '#8b9ab7';

function buildLineChart(canvasId, trendData, unit) {
    unit = unit || '';
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;

    const labels = trendData.labels || [];
    const devices = trendData.devices || [];
    const isLight = document.documentElement.classList.contains('theme-light');

    const datasets = devices.map(function (dev, i) {
        const color = upsRptColor(i);
        const values = (dev.values || []).map(function(v) { return v !== null ? v : null; });
        return {
            label: dev.name,
            data: values,
            borderColor: color.border,
            backgroundColor: color.bg,
            borderWidth: 2,
            pointRadius: 3,
            pointHoverRadius: 6,
            pointBackgroundColor: color.border,
            fill: false,
            tension: 0.3,
            spanGaps: true
        };
    });

    destroyChart(canvasId);
    _charts[canvasId] = new Chart(ctx, {
        type: 'line',
        data: { labels: labels, datasets: datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: { position: 'top', labels: { color: isLight ? '#475569' : '#8b9ab7', usePointStyle: true, padding: 20 } },
                tooltip: {
                    backgroundColor: isLight ? 'rgba(255,255,255,0.98)' : 'rgba(8,11,24,0.96)',
                    titleColor: isLight ? '#0f172a' : '#eef4ff',
                    bodyColor: isLight ? '#475569' : '#8b9ab7',
                    borderColor: isLight ? 'rgba(148,163,184,0.25)' : 'rgba(148,163,184,0.18)',
                    borderWidth: 1,
                    padding: 12,
                    callbacks: {
                        label: function (ctx) {
                            return ctx.dataset.label + ': ' + ctx.parsed.y + ' ' + unit;
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: { color: isLight ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.07)' },
                    ticks: { color: isLight ? '#64748b' : '#4b5a72', maxTicksLimit: 12 }
                },
                y: {
                    grid: { color: isLight ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.07)' },
                    ticks: { color: isLight ? '#64748b' : '#4b5a72' },
                    beginAtZero: false,
                    grace: '5%'
                }
            }
        }
    });
}

function upsRptRenderAll(data) {
    if (typeof upsRptRenderDcpi === 'function') {
        upsRptRenderDcpi(data);
    }
    
    Object.keys(_charts).forEach(function (id) { destroyChart(id); });
    
    if (!data || !data.trends) return;
    var t = data.trends;
    
    if (t.ambient_temp && t.ambient_temp.devices && t.ambient_temp.devices.length > 0) {
        buildLineChart('chartSensorTemp', t.ambient_temp, '°C');
    }
    if (t.humidity && t.humidity.devices && t.humidity.devices.length > 0) {
        buildLineChart('chartSensorHumid', t.humidity, '%');
    }
}

function upsRptApply() {
    var dateF = (document.getElementById('upsRptDateFrom') || {}).value;
    var dateT = (document.getElementById('upsRptDateTo') || {}).value;
    var site = (document.getElementById('upsRptSiteFilter') || {}).value;
    var cat = (document.getElementById('upsRptCategoryFilter') || {}).value || 'ALL';
    var fromH = (document.getElementById('upsRptTimeFromH') || {}).value || '00';
    var fromM = (document.getElementById('upsRptTimeFromM') || {}).value || '00';
    var toH = (document.getElementById('upsRptTimeToH') || {}).value || '23';
    var toM = (document.getElementById('upsRptTimeToM') || {}).value || '59';
    var timeFrom = fromH + ':' + fromM;
    var timeTo = toH + ':' + toM;
    var agg = (document.getElementById('upsRptAggregationFilter') || {}).value || 'average';
    var res = window.UPS_RPT_RESOLUTION || 'daily';
    
    if (!site) return;
    
    window.location.href = '?site_id=' + encodeURIComponent(site) +
        '&date_from=' + encodeURIComponent(dateF || '') +
        '&date_to=' + encodeURIComponent(dateT || '') +
        '&time_from=' + encodeURIComponent(timeFrom) +
        '&time_to=' + encodeURIComponent(timeTo) +
        '&aggregation=' + encodeURIComponent(agg) +
        '&ups_category=' + encodeURIComponent(cat) +
        '&resolution=' + encodeURIComponent(res);
}

function upsRptAjaxReload() {
    var site = window.UPS_RPT_SITE_ID;
    if (!site) return;
    
    var dateF = (document.getElementById('upsRptDateFrom') || {}).value;
    var dateT = (document.getElementById('upsRptDateTo') || {}).value;
    var cat = (document.getElementById('upsRptCategoryFilter') || {}).value || 'ALL';
    var fromH = (document.getElementById('upsRptTimeFromH') || {}).value || '00';
    var fromM = (document.getElementById('upsRptTimeFromM') || {}).value || '00';
    var toH = (document.getElementById('upsRptTimeToH') || {}).value || '23';
    var toM = (document.getElementById('upsRptTimeToM') || {}).value || '59';
    var timeFrom = fromH + ':' + fromM;
    var timeTo = toH + ':' + toM;
    var agg = (document.getElementById('upsRptAggregationFilter') || {}).value || 'average';
    var res = window.UPS_RPT_RESOLUTION || 'daily';
    
    var url = window.UPS_REPORT_API_URL + '?site_id=' + encodeURIComponent(site) +
        '&date_from=' + encodeURIComponent(dateF || '') +
        '&date_to=' + encodeURIComponent(dateT || '') +
        '&time_from=' + encodeURIComponent(timeFrom) +
        '&time_to=' + encodeURIComponent(timeTo) +
        '&aggregation=' + encodeURIComponent(agg) +
        '&ups_category=' + encodeURIComponent(cat) +
        '&resolution=' + encodeURIComponent(res);
        
    var empty = document.getElementById('upsReportEmptyState');
    var loader = document.getElementById('upsRptLoading');
    var content = document.getElementById('upsReportContent');
    
    if (empty) empty.style.display = 'none';
    if (content) content.style.display = 'none';
    if (loader) loader.style.display = 'flex';
    
    fetch(url)
        .then(function(r) { return r.json(); })
        .then(function(json) {
            if (loader) loader.style.display = 'none';
            if (json.status === 'success' && json.data) {
                if (content) content.style.display = 'flex';
                requestAnimationFrame(function () {
                    setTimeout(function () {
                        upsRptRenderAll(json.data);
                    }, 50);
                });
                
                if (json.data && json.data.meta) {
                    var del = document.getElementById('upsRptDeviceCount');
                    if (del) del.textContent = json.data.meta.total_devices || 0;
                    
                    document.querySelectorAll('.ups-rpt-period-text').forEach(function(el) {
                        el.textContent = json.data.meta.period_label || '';
                    });
                }
            } else {
                if (empty) empty.style.display = 'flex';
            }
        })
        .catch(function(e) {
            if (loader) loader.style.display = 'none';
            if (empty) empty.style.display = 'flex';
            console.error(e);
        });
}

function upsRptFilterChange() {
    var site = (document.getElementById('upsRptSiteFilter') || {}).value;
    var btn = document.getElementById('upsRptApplyBtn');
    if (btn) {
        if (site && site !== window.UPS_RPT_SITE_ID) {
            btn.classList.add('pulse');
        } else {
            btn.classList.remove('pulse');
        }
    }
}

function upsRptSetResolution(res, btn) {
    window.UPS_RPT_RESOLUTION = res;
    document.querySelectorAll('.ups-rpt-res-btn').forEach(function (b) {
        b.classList.remove('is-active');
    });
    if (btn) btn.classList.add('is-active');
    
    var dateFromGroup = document.getElementById('upsRptDateFromGroup');
    var dateToGroup = document.getElementById('upsRptDateToGroup');
    var timeGroup = document.getElementById('upsRptTimeGroup');
    var timeGroup2 = document.getElementById('upsRptTimeGroup2');
    var aggGroup = document.getElementById('upsRptAggregationGroup');
    var labelTo = document.getElementById('upsRptDateToLabel');

    if (res === 'daily') {
        if (dateFromGroup) dateFromGroup.style.display = 'none';
        if (dateToGroup) dateToGroup.style.display = '';
        if (labelTo) labelTo.textContent = 'Pilih Tanggal';
        if (timeGroup) timeGroup.style.display = '';
        if (timeGroup2) timeGroup2.style.display = '';
        if (aggGroup) aggGroup.style.display = '';
    } else {
        if (dateFromGroup) dateFromGroup.style.display = '';
        if (dateToGroup) dateToGroup.style.display = '';
        if (labelTo) labelTo.textContent = 'Sampai Tanggal';
        if (timeGroup) timeGroup.style.display = 'none';
        if (timeGroup2) timeGroup2.style.display = 'none';
        if (aggGroup) aggGroup.style.display = 'none';
    }

    var btnApply = document.getElementById('upsRptApplyBtn');
    if (btnApply) btnApply.classList.add('pulse');
}

document.addEventListener('DOMContentLoaded', function() {
    // Auto load if site is provided
    if (window.UPS_REPORT_DATA && window.UPS_REPORT_DATA.trends && Object.keys(window.UPS_REPORT_DATA.trends).length > 0) {
        upsRptRenderAll(window.UPS_REPORT_DATA);
    } else if (window.UPS_RPT_SITE_ID) {
        upsRptAjaxReload();
    }
});
