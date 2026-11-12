
let signalDonutChartInstance = null;
window.activeSignalParam = window.activeSignalParam || 'rsrp';

const SIGNAL_PARAM_RANGES = {
    rsrp: {
        unit: 'dBm',
        excellent: '≥ -85 dBm',
        good: '-85 ~ -100',
        fair: '-100 ~ -105',
        poor: '-105 ~ -115',
        verypoor: '< -115 dBm'
    },
    rssi: {
        unit: 'dBm',
        excellent: '≥ -65 dBm',
        good: '-65 ~ -75',
        fair: '-75 ~ -85',
        poor: '-85 ~ -95',
        verypoor: '< -95 dBm'
    },
    rsrq: {
        unit: 'dB',
        excellent: '≥ -10 dB',
        good: '-10 ~ -15',
        fair: '-15 ~ -18',
        poor: '-18 ~ -20',
        verypoor: '< -20 dB'
    },
    sinr: {
        unit: 'dB',
        excellent: '≥ 20 dB',
        good: '13 ~ 20',
        fair: '0 ~ 13',
        poor: '-5 ~ 0',
        verypoor: '< -5 dB'
    }
};

function classifySignal(param, value) {
    if (value === null || value === undefined || isNaN(Number(value))) return 'unknown';
    const val = Number(value);
    const p = (param || 'rsrp').toLowerCase();
    
    if (p === 'rssi') {
        if (val >= -65) return 'excellent';
        if (val >= -75) return 'good';
        if (val >= -85) return 'fair';
        if (val >= -95) return 'poor';
        return 'verypoor';
    } else if (p === 'rsrq') {
        if (val >= -10) return 'excellent';
        if (val >= -15) return 'good';
        if (val >= -18) return 'fair';
        if (val >= -20) return 'poor';
        return 'verypoor';
    } else if (p === 'sinr') {
        if (val >= 20) return 'excellent';
        if (val >= 13) return 'good';
        if (val >= 0) return 'fair';
        if (val >= -5) return 'poor';
        return 'verypoor';
    } else { // rsrp
        if (val >= -85) return 'excellent';
        if (val >= -100) return 'good';
        if (val >= -105) return 'fair';
        if (val >= -115) return 'poor';
        return 'verypoor';
    }
}

function classifyRsrp(rsrp) {
    return classifySignal('rsrp', rsrp);
}

const SIGNAL_COLORS = {
    excellent: '#3b82f6',
    good: '#22c55e',
    fair: '#eab308',
    poor: '#ef4444',
    verypoor: '#a855f7'
};

function generateDummyRawPoints(count = 120) {
    const points = [];
    const baseLat = -6.28;
    const baseLng = 106.72;
    const now = new Date();

    for (let i = 0; i < count; i++) {
        const t = new Date(now.getTime() - (count - i) * 30000);

        const rand = Math.random();
        let rsrp, rsrq, sinr, rssi;

        if (rand < 0.35) {
            // Excellent
            rsrp = -70 - Math.random() * 15;
            rsrq = -5 - Math.random() * 3;
            sinr = 15 + Math.random() * 15;
            rssi = -50 - Math.random() * 15;
        } else if (rand < 0.60) {
            // Good
            rsrp = -86 - Math.random() * 14;
            rsrq = -8 - Math.random() * 4;
            sinr = 8 + Math.random() * 7;
            rssi = -65 - Math.random() * 15;
        } else if (rand < 0.78) {
            // Fair
            rsrp = -100 - Math.random() * 5;
            rsrq = -12 - Math.random() * 3;
            sinr = 3 + Math.random() * 5;
            rssi = -80 - Math.random() * 10;
        } else if (rand < 0.92) {
            // Poor
            rsrp = -105 - Math.random() * 10;
            rsrq = -15 - Math.random() * 5;
            sinr = -2 + Math.random() * 5;
            rssi = -90 - Math.random() * 10;
        } else {
            // Very Poor
            rsrp = -116 - Math.random() * 10;
            rsrq = -18 - Math.random() * 4;
            sinr = -5 - Math.random() * 5;
            rssi = -100 - Math.random() * 10;
        }

        points.push({
            lat: baseLat + (i * 0.0002) + (Math.random() - 0.5) * 0.0001,
            lng: baseLng + (i * 0.00015) + (Math.random() - 0.5) * 0.0001,
            network_rssi: Math.round(rssi * 10) / 10,
            network_rsrp: Math.round(rsrp * 10) / 10,
            network_rsrq: Math.round(rsrq * 10) / 10,
            network_sinr: Math.round(sinr * 10) / 10,
            speed: Math.round(Math.random() * 60),
            time: t.toISOString()
        });
    }
    return points;
}

function computeSignalStats(rawPoints, paramOverride) {
    const activeParam = (paramOverride || window.activeSignalParam || 'rsrp').toLowerCase();

    // EXCLUDE 0,0 OR GPS ERROR POINTS FROM SIGNAL REPORT AND DONUT CHART STATISTICS
    const validPoints = (rawPoints || []).filter(p => {
        if (!p) return false;
        if (p.lat === 0 && p.lng === 0) return false;
        if (p.gps_status === 'Not Fixed' || p.gps_status === 'Error') return false;
        return true;
    });

    const stats = {
        activeParam: activeParam,
        total: validPoints.length,
        counts: { excellent: 0, good: 0, fair: 0, poor: 0, verypoor: 0 },
        percentages: { excellent: 0, good: 0, fair: 0, poor: 0, verypoor: 0 },
        rssi: { min: Infinity, max: -Infinity, sum: 0, count: 0 },
        rsrp: { min: Infinity, max: -Infinity, sum: 0, count: 0 },
        rsrq: { min: Infinity, max: -Infinity, sum: 0, count: 0 },
        sinr: { min: Infinity, max: -Infinity, sum: 0, count: 0 },
    };

    if (validPoints.length === 0) return stats;

    validPoints.forEach(p => {
        let paramVal = null;
        if (activeParam === 'rssi') paramVal = p.network_rssi;
        else if (activeParam === 'rsrq') paramVal = p.network_rsrq;
        else if (activeParam === 'sinr') paramVal = p.network_sinr;
        else paramVal = p.network_rsrp;

        const level = classifySignal(activeParam, paramVal);
        if (stats.counts[level] !== undefined) {
            stats.counts[level]++;
        }
        if (p.network_rssi != null && !isNaN(p.network_rssi)) {
            const v = Number(p.network_rssi);
            stats.rssi.min = Math.min(stats.rssi.min, v);
            stats.rssi.max = Math.max(stats.rssi.max, v);
            stats.rssi.sum += v;
            stats.rssi.count++;
        }
        if (p.network_rsrp != null && !isNaN(p.network_rsrp)) {
            const v = Number(p.network_rsrp);
            stats.rsrp.min = Math.min(stats.rsrp.min, v);
            stats.rsrp.max = Math.max(stats.rsrp.max, v);
            stats.rsrp.sum += v;
            stats.rsrp.count++;
        }
        if (p.network_rsrq != null && !isNaN(p.network_rsrq)) {
            const v = Number(p.network_rsrq);
            stats.rsrq.min = Math.min(stats.rsrq.min, v);
            stats.rsrq.max = Math.max(stats.rsrq.max, v);
            stats.rsrq.sum += v;
            stats.rsrq.count++;
        }
        if (p.network_sinr != null && !isNaN(p.network_sinr)) {
            const v = Number(p.network_sinr);
            stats.sinr.min = Math.min(stats.sinr.min, v);
            stats.sinr.max = Math.max(stats.sinr.max, v);
            stats.sinr.sum += v;
            stats.sinr.count++;
        }
    });

    // Calculate percentages
    const total = Object.values(stats.counts).reduce((sum, val) => sum + val, 0);
    Object.keys(stats.counts).forEach(key => {
        stats.percentages[key] = total > 0
            ? Math.round((stats.counts[key] / total) * 1000) / 10
            : 0;
    });

    return stats;
}

function computeSignalStatsFromUnits(units) {
    const rawPoints = [];
    if (units && Array.isArray(units)) {
        units.forEach(u => {
            // Ignore units with GPS error (0,0)
            if ((u.lat === 0 && u.lng === 0) || u.has_gps_error || u.gps === 'Not Fixed' || u.gps === 'Error') {
                return;
            }
            const cachedTrack = typeof TRACK_CACHE !== 'undefined' ? TRACK_CACHE[u.id] : null;
            if (cachedTrack && cachedTrack.raw_points && cachedTrack.raw_points.length > 0) {
                rawPoints.push(...cachedTrack.raw_points);
            } else if (u.networkQuality) {
                rawPoints.push({
                    lat: u.lat,
                    lng: u.lng,
                    network_rssi: u.networkQuality.rssi,
                    network_rsrp: u.networkQuality.rsrp,
                    network_rsrq: u.networkQuality.rsrq,
                    network_sinr: u.networkQuality.sinr,
                });
            }
        });
    }
    const stats = computeSignalStats(rawPoints);
    return stats;
}

function renderDonutChart(stats) {
    const canvas = document.getElementById('signalDonutChart');
    if (!canvas) return;

    const data = [
        stats.counts.excellent,
        stats.counts.good,
        stats.counts.fair,
        stats.counts.poor,
        stats.counts.verypoor
    ];

    const hasData = data.some(v => v > 0);

    // If chart already exists, update it instead of destroying/recreating
    if (signalDonutChartInstance) {
        const currentData = signalDonutChartInstance.data.datasets[0].data;
        const targetData = hasData ? data : [1];
        
        // Check if data is identical
        let isIdentical = currentData.length === targetData.length;
        if (isIdentical) {
            for (let i = 0; i < targetData.length; i++) {
                if (currentData[i] !== targetData[i]) {
                    isIdentical = false;
                    break;
                }
            }
        }
        
        // Check if the empty state status changed
        const wasEmpty = signalDonutChartInstance.data.datasets[0].backgroundColor[0] === '#e2e8f0';
        const isEmpty = !hasData;

        if (isIdentical && (wasEmpty === isEmpty)) {
            // Data is the same, no need to update or animate
            return;
        }

        // Update existing chart datasets and options
        signalDonutChartInstance.data.datasets[0].data = targetData;
        signalDonutChartInstance.data.datasets[0].backgroundColor = hasData
            ? [
                SIGNAL_COLORS.excellent,
                SIGNAL_COLORS.good,
                SIGNAL_COLORS.fair,
                SIGNAL_COLORS.poor,
                SIGNAL_COLORS.verypoor
            ]
            : ['#e2e8f0'];
        signalDonutChartInstance.options.plugins.tooltip.enabled = hasData;
        
        // Update smoothly
        signalDonutChartInstance.update();
        return;
    }

    const ctx = canvas.getContext('2d');

    signalDonutChartInstance = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: ['Excellent', 'Good', 'Fair', 'Poor', 'Very Poor'],
            datasets: [{
                data: hasData ? data : [1],
                backgroundColor: hasData
                    ? [
                        SIGNAL_COLORS.excellent,
                        SIGNAL_COLORS.good,
                        SIGNAL_COLORS.fair,
                        SIGNAL_COLORS.poor,
                        SIGNAL_COLORS.verypoor
                    ]
                    : ['#e2e8f0'],
                borderColor: 'white',
                borderWidth: 3,
                borderRadius: 4,
                hoverBorderWidth: 0,
                hoverOffset: 6,
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            cutout: '68%',
            plugins: {
                legend: { display: false },
                tooltip: {
                    enabled: hasData,
                    backgroundColor: '#1e293b',
                    titleFont: { size: 12, weight: '600' },
                    bodyFont: { size: 11 },
                    padding: 10,
                    cornerRadius: 8,
                    callbacks: {
                        label: function (context) {
                            const total = context.dataset.data.reduce((a, b) => a + b, 0);
                            const pct = total > 0 ? ((context.parsed / total) * 100).toFixed(1) : 0;
                            return ` ${context.label}: ${context.parsed} titik (${pct}%)`;
                        }
                    }
                }
            },
            animation: {
                animateRotate: true,
                animateScale: true,
                duration: 1200,
                easing: 'easeOutQuart'
            }
        }
    });
}

function updateBreakdownRanges(activeParam) {
    const p = (activeParam || window.activeSignalParam || 'rsrp').toLowerCase();
    const ranges = SIGNAL_PARAM_RANGES[p] || SIGNAL_PARAM_RANGES['rsrp'];
    const levels = ['excellent', 'good', 'fair', 'poor', 'verypoor'];
    levels.forEach(lvl => {
        const row = document.querySelector(`.signal-bar-row[data-level="${lvl}"] .signal-bar-range`);
        if (row && ranges[lvl]) {
            row.textContent = ranges[lvl];
        }
    });
}

function updateSignalReportUI(stats, unit) {
    const activeParam = stats.activeParam || window.activeSignalParam || 'rsrp';
    window.activeSignalParam = activeParam;

    const nameEl = document.getElementById('signalReportUnitName');
    const dateEl = document.getElementById('signalReportUnitDate');
    const iconEl = document.getElementById('signalReportUnitIcon');
    const siteIconEl = document.getElementById('signalReportSiteIcon');

    if (nameEl) nameEl.textContent = unit?.name || '-';
    if (dateEl) {
        const today = new Date();
        dateEl.textContent = today.toLocaleDateString('id-ID', {
            day: '2-digit',
            month: 'long',
            year: 'numeric'
        });
    }
    
    if (unit && unit.isSite) {
        if (iconEl) iconEl.style.display = 'none';
        if (siteIconEl) siteIconEl.style.display = 'flex';
        const labelEl = document.querySelector('#signalStatTotal .signal-stat-mini-label');
        if (labelEl) labelEl.textContent = 'Units in Site';
        const totalEl = document.querySelector('#signalStatTotal .signal-stat-mini-value');
        if (totalEl) totalEl.textContent = unit.unitCount !== undefined ? unit.unitCount.toLocaleString() : '-';
    } else {
        if (iconEl) iconEl.style.display = 'block';
        if (siteIconEl) siteIconEl.style.display = 'none';
        const labelEl = document.querySelector('#signalStatTotal .signal-stat-mini-label');
        if (labelEl) labelEl.textContent = 'Data Points';
        const totalEl = document.querySelector('#signalStatTotal .signal-stat-mini-value');
        if (totalEl) totalEl.textContent = stats.total.toLocaleString();
        
        if (iconEl && unit) {
            const status = typeof computeDisplayStatus === 'function'
                ? computeDisplayStatus(unit) : (unit.status || 'on');
            const iconUrl = typeof getUnitIcon === 'function'
                ? getUnitIcon(status, unit.fleet_type) : '/assets/img/Tracking/HeavyDutyTruck/HD_Green.png';
            iconEl.src = iconUrl;
        }
    }

    // Summary stats
    const avgRsrpEl = document.querySelector('#signalStatAvgRsrp .signal-stat-mini-value');
    const avgSinrEl = document.querySelector('#signalStatAvgSinr .signal-stat-mini-value');

    if (avgRsrpEl) {
        const avg = stats.rsrp.count > 0 ? (stats.rsrp.sum / stats.rsrp.count).toFixed(1) : '-';
        avgRsrpEl.textContent = avg !== '-' ? `${avg}` : '-';
    }
    if (avgSinrEl) {
        const avg = stats.sinr.count > 0 ? (stats.sinr.sum / stats.sinr.count).toFixed(1) : '-';
        avgSinrEl.textContent = avg !== '-' ? `${avg}` : '-';
    }

    // Update legend ranges for selected parameter
    updateBreakdownRanges(activeParam);

    const coveragePct = stats.percentages.excellent + stats.percentages.good + stats.percentages.fair;
    const coveragePctEl = document.getElementById('signalDonutCenterPct');
    if (coveragePctEl) coveragePctEl.textContent = `${coveragePct.toFixed(0)}%`;

    const levels = ['excellent', 'good', 'fair', 'poor', 'verypoor'];
    const barIds = {
        excellent: 'signalBarExcellent',
        good: 'signalBarGood',
        fair: 'signalBarFair',
        poor: 'signalBarPoor',
        verypoor: 'signalBarverypoor'
    };
    const pctIds = {
        excellent: 'signalPctExcellent',
        good: 'signalPctGood',
        fair: 'signalPctFair',
        poor: 'signalPctPoor',
        verypoor: 'signalPctverypoor'
    };

    levels.forEach(level => {
        const barEl = document.getElementById(barIds[level]);
        const pctEl = document.getElementById(pctIds[level]);
        const pct = stats.percentages[level] || 0;

        if (barEl) {
            setTimeout(() => {
                barEl.style.width = `${pct}%`;
            }, 100);
        }
        if (pctEl) pctEl.textContent = `${pct}%`;
    });

    // Detailed metrics
    updateMetricCard('Rssi', stats.rssi, 'dBm');
    updateMetricCard('Rsrp', stats.rsrp, 'dBm');
    updateMetricCard('Rsrq', stats.rsrq, 'dB');
    updateMetricCard('Sinr', stats.sinr, 'dB');

    // Update active highlight class on metric cards
    document.querySelectorAll('.signal-metric-card').forEach(card => {
        const p = card.dataset.param || 'rsrp';
        if (p.toLowerCase() === activeParam.toLowerCase()) {
            card.classList.add('active');
        } else {
            card.classList.remove('active');
        }
    });

    // Render donut chart
    renderDonutChart(stats);
}

function updateMetricCard(name, metricStats, unit) {
    const minEl = document.getElementById(`metric${name}Min`);
    const maxEl = document.getElementById(`metric${name}Max`);
    const avgEl = document.getElementById(`metric${name}Avg`);

    if (minEl) {
        minEl.textContent = metricStats.count > 0
            ? `Min: ${metricStats.min.toFixed(1)} ${unit}`
            : 'Min: -';
    }
    if (maxEl) {
        maxEl.textContent = metricStats.count > 0
            ? `Max: ${metricStats.max.toFixed(1)} ${unit}`
            : 'Max: -';
    }
    if (avgEl) {
        const avg = metricStats.count > 0
            ? (metricStats.sum / metricStats.count).toFixed(1)
            : '-';
        avgEl.textContent = avg !== '-' ? `Avg: ${avg} ${unit}` : 'Avg: -';
    }
}

// ---- PUBLIC API: Update report from raw_points (for backend integration) ----
/**
 * Call this function to update the signal report panel.
 * @param {Array} rawPoints - Array of raw tracking points with network_rssi, network_rsrp, etc.
 * @param {Object} unit - The selected unit object { name, fleet_type, status, ... }
 */
function updateSignalReport(rawPoints, unit) {
    if (!rawPoints || rawPoints.length === 0) {
        hideSignalReport();
        return;
    }

    const stats = computeSignalStats(rawPoints);
    updateSignalReportUI(stats, unit);
    showSignalReport();
}

/**
 * Call this when a dedicated backend API returns pre-computed stats.
 * Expected format:
 * {
 *   total: 120,
 *   counts: { excellent: 42, good: 30, fair: 22, poor: 17, verypoor: 9 },
 *   percentages: { excellent: 35.0, good: 25.0, fair: 18.3, poor: 14.2, verypoor: 7.5 },
 *   rssi: { min: -110, max: -45, avg: -72 },
 *   rsrp: { min: -125, max: -65, avg: -92 },
 *   rsrq: { min: -22, max: -3, avg: -10 },
 *   sinr: { min: -10, max: 30, avg: 12 }
 * }
 */
function updateSignalReportFromAPI(apiResponse, unit) {
    if (!apiResponse) {
        hideSignalReport();
        return;
    }

    // Transform API format to internal format if needed
    const stats = {
        total: apiResponse.total || 0,
        counts: apiResponse.counts || {},
        percentages: apiResponse.percentages || {},
        rssi: {
            min: apiResponse.rssi?.min ?? Infinity,
            max: apiResponse.rssi?.max ?? -Infinity,
            sum: (apiResponse.rssi?.avg ?? 0) * (apiResponse.total || 1),
            count: apiResponse.total || 0
        },
        rsrp: {
            min: apiResponse.rsrp?.min ?? Infinity,
            max: apiResponse.rsrp?.max ?? -Infinity,
            sum: (apiResponse.rsrp?.avg ?? 0) * (apiResponse.total || 1),
            count: apiResponse.total || 0
        },
        rsrq: {
            min: apiResponse.rsrq?.min ?? Infinity,
            max: apiResponse.rsrq?.max ?? -Infinity,
            sum: (apiResponse.rsrq?.avg ?? 0) * (apiResponse.total || 1),
            count: apiResponse.total || 0
        },
        sinr: {
            min: apiResponse.sinr?.min ?? Infinity,
            max: apiResponse.sinr?.max ?? -Infinity,
            sum: (apiResponse.sinr?.avg ?? 0) * (apiResponse.total || 1),
            count: apiResponse.total || 0
        }
    };

    updateSignalReportUI(stats, unit);
    showSignalReport();
}

// ---- Show / Hide ----
function showSignalReport() {
    const panel = document.getElementById('signalReportPanel');
    if (panel) panel.classList.add('visible');
    
    // Keep button visible and set active state
    const toggleBtn = document.getElementById('signalReportToggleBtn');
    if (toggleBtn) {
        toggleBtn.style.display = ''; // Remove inline
        toggleBtn.classList.remove('hidden-btn');
        toggleBtn.classList.add('active');
        toggleBtn.setAttribute('aria-pressed', 'true');
        toggleBtn.title = 'Close Signal Report';
    }
}

function hideSignalReport() {
    const panel = document.getElementById('signalReportPanel');
    if (panel) panel.classList.remove('visible');
    
    const toggleBtn = document.getElementById('signalReportToggleBtn');
    
    const hasSite = typeof currentSite !== 'undefined' && currentSite;
    const hasUnit = typeof selectedUnit !== 'undefined' && selectedUnit;
    
    if (toggleBtn) {
        toggleBtn.classList.remove('active');
        toggleBtn.setAttribute('aria-pressed', 'false');
        toggleBtn.title = 'Open Signal Report';
        
        if (hasSite || hasUnit) {
            toggleBtn.style.display = ''; // Remove inline
            toggleBtn.classList.remove('hidden-btn');
        } else {
            toggleBtn.classList.add('hidden-btn');
        }
    }
}

// ---- Init ----
window.activeSignalFilters = { excellent: true, good: true, fair: true, poor: true, verypoor: true };

function resetSignalFilters() {
    window.activeSignalFilters = { excellent: true, good: true, fair: true, poor: true, verypoor: true };
    document.querySelectorAll('.signal-bar-row').forEach(row => {
        row.classList.remove('disabled');
    });
    if (typeof window.applySignalFilter === 'function') {
        window.applySignalFilter();
    }
}

function refreshSignalReportAndTrace() {
    if (typeof selectedUnit !== 'undefined' && selectedUnit) {
        const cachedTrack = typeof TRACK_CACHE !== 'undefined' ? TRACK_CACHE[selectedUnit.id] : null;
        const rawPoints = (cachedTrack && cachedTrack.raw_points && cachedTrack.raw_points.length > 0)
            ? cachedTrack.raw_points
            : [];
        if (rawPoints.length > 0) {
            updateSignalReport(rawPoints, selectedUnit);
        } else if (typeof currentSite !== 'undefined' && currentSite) {
            const stats = computeSignalStatsFromUnits(currentSite.units);
            const pseudoUnit = {
                name: currentSite.name || currentSite.code,
                isSite: true,
                unitCount: currentSite.units.length
            };
            updateSignalReportUI(stats, pseudoUnit);
        }
    } else if (typeof currentSite !== 'undefined' && currentSite) {
        const stats = computeSignalStatsFromUnits(currentSite.units);
        const pseudoUnit = {
            name: currentSite.name || currentSite.code,
            isSite: true,
            unitCount: currentSite.units.length
        };
        updateSignalReportUI(stats, pseudoUnit);
    }

    if (typeof window.applySignalFilter === 'function') {
        window.applySignalFilter();
    }
}

document.addEventListener('DOMContentLoaded', function () {
    // Metric cards click handler to switch active parameter (RSSI, RSRP, RSRQ, SINR)
    const metricCards = document.querySelectorAll('.signal-metric-card');
    metricCards.forEach(card => {
        card.addEventListener('click', function() {
            const param = this.dataset.param || 'rsrp';
            if (window.activeSignalParam === param) return;

            window.activeSignalParam = param;

            metricCards.forEach(c => c.classList.remove('active'));
            this.classList.add('active');

            refreshSignalReportAndTrace();
        });
    });

    // Close button handler
    const closeBtn = document.getElementById('signalReportClose');
    if (closeBtn) {
        closeBtn.addEventListener('click', hideSignalReport);
    }
    
    // Legend rows click handler for filtering
    const legendRows = document.querySelectorAll('.signal-bar-row');
    legendRows.forEach(row => {
        row.addEventListener('click', function() {
            const level = this.dataset.level;
            if (!level) return;
            
            // Toggle filter state
            window.activeSignalFilters[level] = !window.activeSignalFilters[level];
            
            // Update UI class
            if (window.activeSignalFilters[level]) {
                this.classList.remove('disabled');
            } else {
                this.classList.add('disabled');
            }
            
            // Trigger filter update on map
            if (typeof window.applySignalFilter === 'function') {
                window.applySignalFilter();
            }
        });
    });

    // Toggle button handler
    const toggleBtn = document.getElementById('signalReportToggleBtn');
    if (toggleBtn) {
        toggleBtn.addEventListener('click', function() {
            const panel = document.getElementById('signalReportPanel');
            if (panel && panel.classList.contains('visible')) {
                // If already visible, just close it
                hideSignalReport();
                return;
            }

            // When clicked to open, re-render the report to make sure it's up to date
            if (typeof selectedUnit !== 'undefined' && selectedUnit) {
                // We're in unit mode, try to use cache
                const cachedTrack = typeof TRACK_CACHE !== 'undefined' ? TRACK_CACHE[selectedUnit.id] : null;
                const rawPoints = (cachedTrack && cachedTrack.raw_points && cachedTrack.raw_points.length > 0)
                    ? cachedTrack.raw_points
                    : generateDummyRawPoints(120);
                updateSignalReport(rawPoints, selectedUnit);
            } else if (typeof currentSite !== 'undefined' && currentSite) {
                // We're in site mode
                const stats = computeSignalStatsFromUnits(currentSite.units);
                const pseudoUnit = {
                    name: currentSite.name || currentSite.code,
                    isSite: true,
                    unitCount: currentSite.units.length
                };
                updateSignalReportUI(stats, pseudoUnit);
                showSignalReport();
            }
        });
    }

    // Hook into selectUnit: override the original selectUnit to also trigger signal report
    const origSelectUnit = window.selectUnit;
    if (typeof origSelectUnit === 'function') {
        window.selectUnit = async function (unitId) {
            // Reset filters on change
            resetSignalFilters();

            // Call original
            await origSelectUnit(unitId);

            // After selecting unit, populate signal report
            // Try to use real track data from cache, fallback to dummy
            setTimeout(() => {
                const cachedTrack = typeof TRACK_CACHE !== 'undefined' ? TRACK_CACHE[unitId] : null;
                const rawPoints = (cachedTrack && cachedTrack.raw_points && cachedTrack.raw_points.length > 0)
                    ? cachedTrack.raw_points
                    : generateDummyRawPoints(120);

                const unit = typeof selectedUnit !== 'undefined' ? selectedUnit : null;
                updateSignalReport(rawPoints, unit);
            }, 800); // Short delay to let track data load
        };
    }

    // Also hook into site/area changes
    const origSelectSite = window.selectSite;
    if (typeof origSelectSite === 'function') {
        window.selectSite = function (siteCode) {
            // Reset filters on change
            resetSignalFilters();

            // Hide temporarily while switching
            hideSignalReport();
            origSelectSite(siteCode);
            
            // Show site level report
            setTimeout(() => {
                if (typeof currentSite !== 'undefined' && currentSite) {
                    const stats = computeSignalStatsFromUnits(currentSite.units);
                    const pseudoUnit = {
                        name: currentSite.name || currentSite.code,
                        isSite: true,
                        unitCount: currentSite.units.length
                    };
                    updateSignalReportUI(stats, pseudoUnit);
                    showSignalReport();
                }
            }, 1500); // Give tracking.js showSiteLevelTracks time to fetch data
        };
    }

    const origSelectArea = window.selectArea;
    if (typeof origSelectArea === 'function') {
        window.selectArea = function (areaKey) {
            // Reset filters on change
            resetSignalFilters();

            hideSignalReport();
            origSelectArea(areaKey);
        };
    }

    // Periodic refresh to keep UI synced with polling
    setInterval(() => {
        const panel = document.getElementById('signalReportPanel');
        if (panel && panel.classList.contains('visible')) {
            if (typeof selectedUnit !== 'undefined' && selectedUnit) {
                // For unit mode, tracking.js doesn't currently poll tracking history, 
                // so the report is mostly static unless we fetch new history.
                // Could be updated if realtime single point is appended.
            } else if (typeof currentSite !== 'undefined' && currentSite) {
                // Update site mode dynamically when units update their network stats
                const stats = computeSignalStatsFromUnits(currentSite.units);
                const pseudoUnit = {
                    name: currentSite.name || currentSite.code,
                    isSite: true,
                    unitCount: currentSite.units.length
                };
                updateSignalReportUI(stats, pseudoUnit);
            }
        }
    }, 5000);
});
