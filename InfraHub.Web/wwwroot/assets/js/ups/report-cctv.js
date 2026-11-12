/**
 * CCTV Report & Analytics JavaScript
 * Pure report charts: Binary ON/OFF State Timeline and Uptime vs Downtime Duration Comparison
 */

const CCTV_COLORS = [
    { border: '#10b981', bg: 'rgba(16, 185, 129, 0.15)', solid: '#10b981' },
    { border: '#3b82f6', bg: 'rgba(59, 130, 246, 0.15)', solid: '#3b82f6' },
    { border: '#a855f7', bg: 'rgba(168, 85, 247, 0.15)', solid: '#a855f7' },
    { border: '#f59e0b', bg: 'rgba(245, 158, 11, 0.15)', solid: '#f59e0b' },
    { border: '#ec4899', bg: 'rgba(236, 72, 153, 0.15)', solid: '#ec4899' },
];

function cctvColor(idx) {
    return CCTV_COLORS[idx % CCTV_COLORS.length];
}

const _cctvCharts = {};

function destroyCctvChart(id) {
    if (_cctvCharts[id]) {
        _cctvCharts[id].destroy();
        delete _cctvCharts[id];
    }
}

// Chart.js global defaults
const initialTheme = localStorage.getItem('ups-theme') || 'dark';
if (typeof Chart !== 'undefined') {
    Chart.defaults.font.family = "'Inter', 'Segoe UI', system-ui, sans-serif";
    Chart.defaults.color = initialTheme === 'light' ? '#64748b' : '#8b9ab7';
}

// 1. TIMELINE LINE CHART (BINARY ON / OFF)
function buildCctvTimelineChart(canvasId, trendData) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;

    const labels = trendData.labels || [];
    const devices = trendData.devices || [];
    const isLight = document.documentElement.classList.contains('theme-light');

    const datasets = devices.map((dev, i) => {
        const color = cctvColor(i);
        const values = (dev.values || []).map(v => (v !== null ? v : null));

        return {
            label: dev.name,
            data: values,
            borderColor: color.border,
            backgroundColor: color.bg,
            borderWidth: 2.5,
            stepped: 'before',
            pointRadius: 2,
            pointHoverRadius: 6,
            pointBackgroundColor: color.solid,
            fill: true,
            spanGaps: true
        };
    });

    destroyCctvChart(canvasId);

    _cctvCharts[canvasId] = new Chart(ctx, {
        type: 'line',
        data: { labels: labels, datasets: datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: {
                    position: 'top',
                    labels: {
                        color: isLight ? '#475569' : '#8b9ab7',
                        usePointStyle: true,
                        padding: 16,
                        font: { size: 12, weight: 600 }
                    }
                },
                tooltip: {
                    backgroundColor: isLight ? 'rgba(255,255,255,0.98)' : 'rgba(8,11,24,0.96)',
                    titleColor: isLight ? '#0f172a' : '#eef4ff',
                    bodyColor: isLight ? '#475569' : '#8b9ab7',
                    borderColor: isLight ? 'rgba(148,163,184,0.25)' : 'rgba(148,163,184,0.18)',
                    borderWidth: 1,
                    padding: 12,
                    callbacks: {
                        label: function (tooltipItem) {
                            const val = tooltipItem.parsed.y;
                            if (val === null || isNaN(val)) return tooltipItem.dataset.label + ': Tidak Ada Data';
                            const statusStr = val >= 1 ? '● ON (Aktif / Terhubung)' : '● OFF (Mati / Terputus)';
                            return `${tooltipItem.dataset.label}: ${statusStr}`;
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: { color: isLight ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.06)' },
                    ticks: { color: isLight ? '#64748b' : '#4b5a72', maxTicksLimit: 14 }
                },
                y: {
                    min: -0.15,
                    max: 1.15,
                    grid: { color: isLight ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.06)' },
                    ticks: {
                        color: isLight ? '#64748b' : '#4b5a72',
                        callback: function (val) {
                            if (val === 0) return '● OFF (Mati)';
                            if (val === 1) return '● ON (Aktif)';
                            return '';
                        }
                    }
                }
            }
        }
    });
}

// 2. DURATION COMPARISON BAR CHART (UPTIME VS DOWNTIME)
function buildCctvDurationBarChart(canvasId, compData) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;

    const devNames = compData.device_names || [];
    const uptimeMins = compData.uptime_minutes || [];
    const downtimeMins = compData.downtime_minutes || [];
    const uptimeLabels = compData.uptime_labels || [];
    const downtimeLabels = compData.downtime_labels || [];
    const isLight = document.documentElement.classList.contains('theme-light');

    destroyCctvChart(canvasId);

    _cctvCharts[canvasId] = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: devNames,
            datasets: [
                {
                    label: 'Total Waktu Uptime (ON)',
                    data: uptimeMins,
                    backgroundColor: 'rgba(34, 197, 94, 0.75)',
                    borderColor: '#22c55e',
                    borderWidth: 1.5,
                    borderRadius: 8,
                    maxBarThickness: 45
                },
                {
                    label: 'Total Waktu Downtime (OFF)',
                    data: downtimeMins,
                    backgroundColor: 'rgba(239, 68, 68, 0.75)',
                    borderColor: '#ef4444',
                    borderWidth: 1.5,
                    borderRadius: 8,
                    maxBarThickness: 45
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: {
                    position: 'top',
                    labels: {
                        color: isLight ? '#475569' : '#8b9ab7',
                        usePointStyle: true,
                        padding: 16,
                        font: { size: 12, weight: 600 }
                    }
                },
                tooltip: {
                    backgroundColor: isLight ? 'rgba(255,255,255,0.98)' : 'rgba(8,11,24,0.96)',
                    titleColor: isLight ? '#0f172a' : '#eef4ff',
                    bodyColor: isLight ? '#475569' : '#8b9ab7',
                    borderColor: isLight ? 'rgba(148,163,184,0.25)' : 'rgba(148,163,184,0.18)',
                    borderWidth: 1,
                    padding: 12,
                    callbacks: {
                        label: function (ctx) {
                            const idx = ctx.dataIndex;
                            const isUp = ctx.datasetIndex === 0;
                            const durText = isUp ? (uptimeLabels[idx] || ctx.raw + ' mnt') : (downtimeLabels[idx] || ctx.raw + ' mnt');
                            return ` ${ctx.dataset.label}: ${durText} (${ctx.raw} menit)`;
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: { color: isLight ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.06)' },
                    ticks: { color: isLight ? '#64748b' : '#4b5a72', font: { weight: 600 } }
                },
                y: {
                    min: 0,
                    grid: { color: isLight ? 'rgba(148,163,184,0.12)' : 'rgba(148,163,184,0.06)' },
                    ticks: {
                        color: isLight ? '#64748b' : '#4b5a72',
                        callback: function (val) {
                            if (val >= 60) {
                                const h = Math.floor(val / 60);
                                const m = val % 60;
                                return m > 0 ? `${h}j ${m}m` : `${h} jam`;
                            }
                            return val + ' mnt';
                        }
                    }
                }
            }
        }
    });
}

function upsRptRenderAll(data) {
    if (typeof upsRptRenderDcpi === 'function') {
        upsRptRenderDcpi(data);
    }

    if (!data) return;

    // Update Summary Stats (3 Clean Cards)
    const stats = data.stats || {};
    $('#statCctvTotal').text((stats.total_records || 0) + ' Data');
    $('#statCctvUptime').text(stats.uptime_duration || '0 dtk');
    $('#statCctvDowntime').text(stats.downtime_duration || '0 dtk');

    // Update Device Count & Period Labels
    if (data.meta) {
        $('#upsRptDeviceCount').text(data.meta.total_devices || 0);
        $('.ups-rpt-period-text').text(data.meta.period_label || '-');
    }

    // Render Timeline Chart (ON / OFF)
    if (data.trends && data.trends.timeline) {
        buildCctvTimelineChart('chartCctvTimeline', data.trends.timeline);
    }

    // Render Duration Comparison Bar Chart (Uptime vs Downtime)
    if (data.trends && data.trends.comparison) {
        buildCctvDurationBarChart('chartCctvDuration', data.trends.comparison);
    }
}

// Resolution Switcher
function upsRptSetResolution(res, btn) {
    $('.ups-rpt-res-btn').removeClass('is-active');
    $(btn).addClass('is-active');
    window.UPS_RPT_RESOLUTION = res;

    if (res === 'daily') {
        $('#upsRptDateFromGroup').hide();
        $('#upsRptDateToGroup').show();
        $('#upsRptDateToLabel').text('Pilih Tanggal');
        $('#upsRptMonthGroup').hide();
        $('#upsRptTimeGroup').show();
        $('#upsRptTimeGroup2').show();
        $('#upsRptAggregationGroup').hide();
    } else if (res === 'weekly') {
        $('#upsRptDateFromGroup').show();
        $('#upsRptDateToGroup').show();
        $('#upsRptDateToLabel').text('Sampai Tanggal');
        $('#upsRptMonthGroup').hide();
        $('#upsRptTimeGroup').hide();
        $('#upsRptTimeGroup2').hide();
        $('#upsRptAggregationGroup').hide();
    } else if (res === 'monthly') {
        $('#upsRptDateFromGroup').show();
        $('#upsRptDateToGroup').show();
        $('#upsRptDateToLabel').text('Sampai Tanggal');
        $('#upsRptMonthGroup').hide();
        $('#upsRptTimeGroup').hide();
        $('#upsRptTimeGroup2').hide();
        $('#upsRptAggregationGroup').hide();
    }
}

function upsRptFilterChange() {
    // Optional instant feedback
}

function upsRptApply() {
    const siteId = $('#upsRptSiteFilter').val();
    if (!siteId) {
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                icon: 'warning',
                title: 'Pilih Site',
                text: 'Silakan pilih site terlebih dahulu untuk melihat report CCTV.',
                background: '#0f172a',
                color: '#e2e8f0'
            });
        } else {
            alert('Pilih site terlebih dahulu.');
        }
        return;
    }

    const resolution = window.UPS_RPT_RESOLUTION || 'daily';
    const dateFrom = $('#upsRptDateFrom').val();
    const dateTo = $('#upsRptDateTo').val();
    const timeFrom = ($('#upsRptTimeFromH').val() || '00') + ':' + ($('#upsRptTimeFromM').val() || '00');
    const timeTo = ($('#upsRptTimeToH').val() || '23') + ':' + ($('#upsRptTimeToM').val() || '59');
    const aggregation = $('#upsRptAggregationFilter').val() || 'actual';

    $('#upsReportEmptyState').hide();
    $('#upsReportContent').hide();
    $('#upsRptLoading').show();

    const params = new URLSearchParams({
        site_id: siteId,
        resolution: resolution,
        date_from: dateFrom,
        date_to: dateTo,
        time_from: timeFrom,
        time_to: timeTo,
        aggregation: aggregation
    });

    const apiUrl = window.UPS_REPORT_API_URL || '/api/ups/report/cctv';

    $.ajax({
        url: apiUrl + '?' + params.toString(),
        type: 'GET',
        success: function (res) {
            $('#upsRptLoading').hide();
            if (res.status === 'success' && res.data) {
                window.UPS_REPORT_DATA = res.data;
                const totalDev = (res.data.meta && res.data.meta.total_devices) || 0;
                const totalRecords = (res.data.stats && res.data.stats.total_records) || 0;

                if (totalDev > 0 || totalRecords > 0) {
                    $('#upsReportContent').css('display', 'flex').show();
                    upsRptRenderAll(res.data);
                } else {
                    $('#upsReportEmptyState').show();
                    $('#upsReportEmptyState .ups-rpt-empty-state__title').text('Tidak Ada Data CCTV');
                    $('#upsReportEmptyState .ups-rpt-empty-state__sub').text('Tidak ditemukan perangkat CCTV atau catatan log pada periode ini.');
                }
            } else {
                $('#upsReportEmptyState').show();
            }
        },
        error: function (xhr) {
            $('#upsRptLoading').hide();
            const msg = xhr.responseJSON?.message || 'Gagal memuat data report CCTV.';
            if (typeof Swal !== 'undefined') {
                Swal.fire({ icon: 'error', title: 'Error', text: msg, background: '#0f172a', color: '#e2e8f0' });
            } else {
                alert(msg);
            }
        }
    });
}

// Global window bindings
window.upsRptApply = upsRptApply;
window.upsRptSetResolution = upsRptSetResolution;
window.upsRptFilterChange = upsRptFilterChange;
window.upsRptRenderAll = upsRptRenderAll;
