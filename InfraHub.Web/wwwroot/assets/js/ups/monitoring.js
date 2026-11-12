
document.addEventListener('DOMContentLoaded', function () {

    // DUMMY DATA GENERATORS
    function generateSparklineData(base, variance, count) {
        var data = [];
        for (var i = 0; i < count; i++) {
            data.push(base + (Math.random() - 0.5) * variance);
        }
        return data;
    }

    function generateTimeLabels() {
        var labels = [];
        for (var h = 0; h <= 23; h += 3) {
            labels.push(String(h).padStart(2, '0') + ':00');
        }
        return labels;
    }

    function generateChartData(base, variance, count) {
        var data = [];
        for (var i = 0; i < count; i++) {
            data.push(base + (Math.random() - 0.5) * variance);
        }
        return data;
    }

    // SPARKLINE RENDERER (Canvas API)
    function drawSparkline(canvasId, data, color, fillColor) {
        var canvas = document.getElementById(canvasId);
        if (!canvas) return;

        var ctx = canvas.getContext('2d');
        var dpr = window.devicePixelRatio || 1;
        var rect = canvas.parentElement.getBoundingClientRect();

        canvas.width = rect.width * dpr;
        canvas.height = rect.height * dpr;
        canvas.style.width = rect.width + 'px';
        canvas.style.height = rect.height + 'px';
        ctx.scale(dpr, dpr);

        var w = rect.width;
        var h = rect.height;
        var padding = 4;

        var min = Math.min.apply(null, data);
        var max = Math.max.apply(null, data);
        var range = max - min || 1;

        var stepX = (w - padding * 2) / (data.length - 1);

        // Draw fill
        ctx.beginPath();
        ctx.moveTo(padding, h - padding);
        for (var i = 0; i < data.length; i++) {
            var x = padding + i * stepX;
            var y = h - padding - ((data[i] - min) / range) * (h - padding * 2);
            if (i === 0) ctx.lineTo(x, y);
            else ctx.lineTo(x, y);
        }
        ctx.lineTo(padding + (data.length - 1) * stepX, h - padding);
        ctx.closePath();

        var gradient = ctx.createLinearGradient(0, 0, 0, h);
        gradient.addColorStop(0, fillColor || 'rgba(129, 140, 248, 0.2)');
        gradient.addColorStop(1, 'rgba(129, 140, 248, 0)');
        ctx.fillStyle = gradient;
        ctx.fill();

        // Draw line
        ctx.beginPath();
        for (var i = 0; i < data.length; i++) {
            var x = padding + i * stepX;
            var y = h - padding - ((data[i] - min) / range) * (h - padding * 2);
            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        }
        ctx.strokeStyle = color || '#818cf8';
        ctx.lineWidth = 2;
        ctx.lineJoin = 'round';
        ctx.lineCap = 'round';
        ctx.stroke();
    }

    // GROUND SYSTEM 
    function updateGroundStatus(value) {
        var card = document.getElementById('ups-card-ground');
        var statusBadge = document.getElementById('ups-ground-status-badge');
        var statusDot = document.getElementById('ups-ground-status-dot');
        var thresholdFill = document.getElementById('ups-threshold-fill');

        if (!statusBadge || !statusDot || !thresholdFill) return;

        var status, statusClass;
        var hasValue = value !== null && value !== undefined && value !== '' && Number.isFinite(Number(value));

        if (!hasValue) {
            status = 'Error';
            statusClass = 'error';
        } else if (value < 3) {
            status = 'Normal';
            statusClass = 'normal';
        } else if (value < 5) {
            status = 'Warning';
            statusClass = 'warning';
        } else {
            status = 'Critical';
            statusClass = 'critical';
        }

        // Update badge
        statusBadge.textContent = status;
        statusBadge.className = 'ups-status-badge ups-status-badge--' + statusClass;

        // Update dot
        statusDot.className = 'ups-status-dot ups-status-dot--' + statusClass;

        if (card) {
            card.classList.remove('ups-glass-card--normal', 'ups-glass-card--warning', 'ups-glass-card--critical', 'ups-glass-card--error');
            card.classList.add('ups-glass-card--' + statusClass);
        }

        // Update threshold bar (max 5Ω = 100%)
        var percent = hasValue ? Math.min((value / 5) * 100, 100) : 0;
        thresholdFill.style.width = percent + '%';
        thresholdFill.className = 'ups-threshold-fill ups-threshold-fill--' + statusClass;
    }

    var mainChart = null;
    var selectionPlaceholder = document.getElementById('ups-selection-placeholder');
    var upsContent = document.getElementById('ups-content');
    var monitoringDefaultsRendered = false;
    var autoRefreshTimer = null;
    var UPS_REFRESH_INTERVAL_MS = 60000;

    // MAIN CHART 
    function initMainChart(labels, inputData, outputData) {
        var canvas = document.getElementById('upsMainChart');
        if (!canvas || typeof Chart === 'undefined') return;

        var ctx = canvas.getContext('2d');

        // Gradient for Input
        var inputGradient = ctx.createLinearGradient(0, 0, 0, 300);
        inputGradient.addColorStop(0, 'rgba(129, 140, 248, 0.15)');
        inputGradient.addColorStop(1, 'rgba(129, 140, 248, 0)');

        // Gradient for Output
        var outputGradient = ctx.createLinearGradient(0, 0, 0, 300);
        outputGradient.addColorStop(0, 'rgba(248, 113, 113, 0.15)');
        outputGradient.addColorStop(1, 'rgba(248, 113, 113, 0)');

        var isL = (localStorage.getItem('ups-theme') || 'dark') === 'light';
        var themeBg = isL ? '#f8fafc' : '#0a0e1a';

        if (mainChart) {
            mainChart.data.labels = labels;
            mainChart.data.datasets[0].data = inputData;
            mainChart.data.datasets[0].pointBorderColor = themeBg;
            mainChart.data.datasets[1].data = outputData;
            mainChart.data.datasets[1].pointBorderColor = themeBg;
            mainChart.update();
            return;
        }

        mainChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Input',
                        data: inputData,
                        spanGaps: true,
                        borderColor: '#818cf8',
                        backgroundColor: inputGradient,
                        borderWidth: 2,
                        fill: true,
                        tension: 0.3,
                        pointBackgroundColor: '#818cf8',
                        pointBorderColor: themeBg,
                        pointBorderWidth: 2,
                        pointRadius: 5,
                        pointHoverRadius: 7,
                        pointHoverBackgroundColor: '#a5b4fc',
                        pointHoverBorderColor: '#fff',
                        pointHoverBorderWidth: 2,
                    },
                    {
                        label: 'Output',
                        data: outputData,
                        spanGaps: true,
                        borderColor: '#f87171',
                        backgroundColor: outputGradient,
                        borderWidth: 2,
                        fill: true,
                        tension: 0.3,
                        pointBackgroundColor: '#f87171',
                        pointBorderColor: themeBg,
                        pointBorderWidth: 2,
                        pointRadius: 5,
                        pointHoverRadius: 7,
                        pointHoverBackgroundColor: '#fca5a5',
                        pointHoverBorderColor: '#fff',
                        pointHoverBorderWidth: 2,
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    intersect: false,
                    mode: 'index',
                },
                plugins: {
                    legend: {
                        display: false,
                    },
                    tooltip: {
                        backgroundColor: isL ? 'rgba(255, 255, 255, 0.98)' : 'rgba(15, 23, 42, 0.95)',
                        titleColor: isL ? '#0f172a' : '#e2e8f0',
                        bodyColor: isL ? '#475569' : '#cbd5e1',
                        borderColor: isL ? 'rgba(148, 163, 184, 0.22)' : 'rgba(139, 92, 246, 0.3)',
                        borderWidth: 1,
                        cornerRadius: 10,
                        padding: 12,
                        titleFont: { size: 13, weight: '600' },
                        bodyFont: { size: 12 },
                        displayColors: true,
                        boxPadding: 4,
                        callbacks: {
                            label: function (context) {
                                return ' ' + context.dataset.label + ': ' + context.parsed.y.toFixed(1) + ' V';
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            color: isL ? 'rgba(148,163,184,0.12)' : 'rgba(255, 255, 255, 0.04)',
                            drawBorder: false,
                        },
                        ticks: {
                            color: isL ? '#64748b' : '#475569',
                            font: { size: 11, weight: '500' },
                            maxRotation: 0,
                        },
                        border: {
                            display: false,
                        }
                    },
                    y: {
                        min: 0,
                        max: 300,
                        grid: {
                            color: isL ? 'rgba(148,163,184,0.12)' : 'rgba(255, 255, 255, 0.04)',
                            drawBorder: false,
                        },
                        ticks: {
                            color: isL ? '#64748b' : '#475569',
                            font: { size: 11, weight: '500' },
                            stepSize: 50,
                            callback: function (value) {
                                return value;
                            }
                        },
                        border: {
                            display: false,
                        }
                    }
                },
                animation: {
                    duration: 1200,
                    easing: 'easeInOutQuart',
                }
            }
        });
    }

    // INITIALIZE

    // INITIALIZE

    function safeNumber(value, fallback) {
        var n = Number(value);
        return Number.isFinite(n) ? n : fallback;
    }

    function normalizeNullableNumber(value) {
        if (value === null || value === undefined || value === '') {
            return null;
        }

        var n = Number(value);
        return Number.isFinite(n) ? n : null;
    }

    function getAuthHeaders() {
        var token = localStorage.getItem('access_token');
        var headers = { 'Accept': 'application/json' };
        if (token) {
            headers.Authorization = 'Bearer ' + token;
        }
        return headers;
    }

    function isTimestampStale(timestamp, thresholdMs) {
        if (!timestamp) return false;
        var sampleDate = new Date(timestamp);
        if (isNaN(sampleDate.getTime())) return false;
        thresholdMs = thresholdMs || (15 * 60 * 1000);
        var diff = new Date().getTime() - sampleDate.getTime();
        if (diff > thresholdMs) {
            var tzOffsetMs = new Date().getTimezoneOffset() * 60 * 1000;
            if (Math.abs(diff + tzOffsetMs) <= thresholdMs) {
                return false;
            }
            return true;
        }
        return false;
    }

    function normalizeUpsData(raw) {
        var data = raw ? JSON.parse(JSON.stringify(raw)) : {};

        var isOffline = false;
        if (data.is_active === 0 || data.is_active === "0" || data.is_active === false) {
            isOffline = true;
        }

        if (data.ups_status && data.ups_status.toLowerCase() === 'offline') {
            isOffline = true;
        }

        if (data.device_time) {
            if (isTimestampStale(data.device_time, 15 * 60 * 1000)) {
                isOffline = true;
            }
        }

        if (isOffline) {
            data.ups_status = 'Offline';
            var zeroFields = ['voltage_in_a', 'voltage_in_b', 'voltage_in_c', 'current_in_a', 'current_in_b', 'current_in_c', 'input_frequency', 'voltage_out_a', 'voltage_out_b', 'voltage_out_c', 'current_out_a', 'current_out_b', 'current_out_c', 'output_frequency', 'battery_percent', 'battery_voltage', 'battery_runtime_min', 'load_percent', 'total_watts', 'ambient_temp', 'room_temp'];
            zeroFields.forEach(function (f) { data[f] = 0; });
            data.grounding_resistance = null;
        }

        var inA = safeNumber(data.current_in_a, 0);
        var inB = safeNumber(data.current_in_b, 0);
        var inC = safeNumber(data.current_in_c, 0);
        var outA = safeNumber(data.current_out_a, 0);
        var outB = safeNumber(data.current_out_b, 0);
        var outC = safeNumber(data.current_out_c, 0);

        var pt = data.ups_phase_type || '3/3';
        var inCount = (pt === '1/1') ? 1 : 3;
        var outCount = (pt === '3/3') ? 3 : 1;

        data.input_ampere = safeNumber(data.input_ampere, (inA + inB + inC) / inCount);
        data.output_ampere = safeNumber(data.output_ampere, (outA + outB + outC) / outCount);
        data.input_freq = safeNumber(data.input_frequency, safeNumber(data.input_freq, 0));
        data.output_freq = safeNumber(data.output_frequency, safeNumber(data.output_freq, 0));
        data.grounding_resistance = normalizeNullableNumber(data.grounding_resistance);
        data.ups_status = data.ups_status || 'Unknown';

        return data;
    }

    // DUMMY DATABASE ROW MATCHING DB COLUMNS
    var mockDbData = normalizeUpsData({
        voltage_in_a: 220.1,
        voltage_in_b: 221.5,
        voltage_in_c: 219.8,
        current_in_a: 15.0,
        current_in_b: 15.4,
        current_in_c: 15.1,
        input_frequency: 50.1,
        voltage_out_a: 220.0,
        voltage_out_b: 220.2,
        voltage_out_c: 219.9,
        current_out_a: 14.8,
        current_out_b: 14.7,
        current_out_c: 14.9,
        output_frequency: 50.0,
        battery_percent: 85,
        battery_voltage: 54.2,
        battery_runtime_min: 45,
        loadPercent: 31,
        total_watts: 2450,
        ambient_temp: 32,
        room_temp: 28,
        ups_status: "On",
        grounding_resistance: 2.0
    });

    var inputSparkData = generateSparklineData(220, 10, 12);
    var outputSparkData = generateSparklineData(220, 8, 12);
    var groundSparkData = generateSparklineData(2.0, 1.5, 12);
    var upsTempSparkData = generateSparklineData(mockDbData.ambient_temp, 4, 12);
    var roomTempSparkData = generateSparklineData(mockDbData.room_temp, 3, 12);
    var tempSparkData = upsTempSparkData;
    var wattSparkData = generateSparklineData(mockDbData.total_watts, 300, 12);

    function formatMonitoringDowntime(lastOnlineTimeStr) {
        if (!lastOnlineTimeStr) return "N/A";
        var lastTime = new Date(lastOnlineTimeStr).getTime();
        if (isNaN(lastTime)) return "N/A";
        var diffMs = new Date().getTime() - lastTime;
        var tzOffsetMs = new Date().getTimezoneOffset() * 60 * 1000;
        if (Math.abs(diffMs + tzOffsetMs) < diffMs) {
            diffMs = Math.max(0, diffMs + tzOffsetMs);
        }
        if (diffMs < 0) return "0m";
        var diffMins = Math.floor(diffMs / (1000 * 60));
        if (diffMins < 60) return diffMins + "m";
        var diffHours = Math.floor(diffMins / 60);
        var remMins = diffMins % 60;
        if (diffHours < 24) return diffHours + "j " + remMins + "m";
        var diffDays = Math.floor(diffHours / 24);
        var remHours = diffHours % 24;
        return diffDays + "h " + remHours + "j";
    }

    function updateOfflineStatus(data) {
        var st = (data.ups_status || "").toLowerCase();
        var isExplicitOffline = st === 'offline' || st === 'connection error' || st === 'auth error' || st === 'system error';
        var sampleTime = data.latest_sample_at || data.device_time || data.created_at;
        var isStale = isTimestampStale(sampleTime, 15 * 60 * 1000);
        var isDead = isExplicitOffline || isStale;

        var overlay = document.getElementById('ups-offline-overlay');
        var cards = document.querySelectorAll('.ups-glass-card');

        if (overlay) {
            if (isDead) {
                overlay.style.display = 'block';
                var statusTitle = document.getElementById('ups-offline-status-text');
                if (statusTitle) {
                    statusTitle.textContent = isExplicitOffline ? ('STATUS PERANGKAT: ' + String(data.ups_status).toUpperCase()) : 'STATUS PERANGKAT: STALE DATA (> 15 MENIT)';
                }
                var ipEl = document.getElementById('ups-offline-ip');
                if (ipEl) ipEl.textContent = data.ip_address || data.ip || '-';

                var dtPill = document.getElementById('ups-offline-downtime-text');
                var refTime = data.last_online_time || data.latest_sample_at || data.device_time;
                if (dtPill) {
                    if (refTime) {
                        var dStr = formatMonitoringDowntime(refTime);
                        var timeOnly = "";
                        try {
                            var dObj = new Date(refTime);
                            if (!isNaN(dObj.getTime())) {
                                timeOnly = dObj.toLocaleString('id-ID', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
                            }
                        } catch(e){}
                        dtPill.textContent = 'Down: ' + dStr + (timeOnly ? (' (Sejak ' + timeOnly + ')') : '');
                    } else {
                        dtPill.textContent = 'Down: Perangkat Offline';
                    }
                }

                cards.forEach(function(c) { c.classList.add('ups-glass-card--offline'); });
            } else {
                overlay.style.display = 'none';
                cards.forEach(function(c) { c.classList.remove('ups-glass-card--offline'); });
            }
        }
    }

    function renderUPSData(data) {
        updateOfflineStatus(data);

        var pt = data.ups_phase_type || '3/3';
        var inCount = (pt === '1/1') ? 1 : 3;
        var outCount = (pt === '3/3') ? 3 : 1;

        // UPS Input
        var avgIn = ((safeNumber(data.voltage_in_a, 0) + safeNumber(data.voltage_in_b, 0) + safeNumber(data.voltage_in_c, 0)) / inCount).toFixed(0);
        var inEl = document.getElementById('ups-ups-input-voltage');
        if (inEl) inEl.textContent = avgIn;
        var inA = document.getElementById('ups-input-voltage-a');
        if (inA) inA.textContent = safeNumber(data.voltage_in_a, 0).toFixed(0) + 'V';
        var inB = document.getElementById('ups-input-voltage-b');
        if (inB) {
            if (inCount === 3) {
                inB.textContent = safeNumber(data.voltage_in_b, 0).toFixed(0) + 'V';
                if (inB.closest('.ups-badge')) inB.closest('.ups-badge').style.display = '';
            } else {
                if (inB.closest('.ups-badge')) inB.closest('.ups-badge').style.display = 'none';
            }
        }
        var inC = document.getElementById('ups-input-voltage-c');
        if (inC) {
            if (inCount === 3) {
                inC.textContent = safeNumber(data.voltage_in_c, 0).toFixed(0) + 'V';
                if (inC.closest('.ups-badge')) inC.closest('.ups-badge').style.display = '';
            } else {
                if (inC.closest('.ups-badge')) inC.closest('.ups-badge').style.display = 'none';
            }
        }

        var inAmp = document.getElementById('ups-input-ampere');
        if (inAmp) inAmp.textContent = safeNumber(data.input_ampere, 0).toFixed(0);
        var inFreq = document.getElementById('ups-input-freq');
        if (inFreq) inFreq.textContent = safeNumber(data.input_freq, 0).toFixed(1);

        // UPS Output
        var avgOut = ((safeNumber(data.voltage_out_a, 0) + safeNumber(data.voltage_out_b, 0) + safeNumber(data.voltage_out_c, 0)) / outCount).toFixed(0);
        var outEl = document.getElementById('ups-ups-output-voltage');
        if (outEl) outEl.textContent = avgOut;
        var outA = document.getElementById('ups-output-voltage-a');
        if (outA) outA.textContent = safeNumber(data.voltage_out_a, 0).toFixed(0) + 'V';
        var outB = document.getElementById('ups-output-voltage-b');
        if (outB) {
            if (outCount === 3) {
                outB.textContent = safeNumber(data.voltage_out_b, 0).toFixed(0) + 'V';
                if (outB.closest('.ups-badge')) outB.closest('.ups-badge').style.display = '';
            } else {
                if (outB.closest('.ups-badge')) outB.closest('.ups-badge').style.display = 'none';
            }
        }
        var outC = document.getElementById('ups-output-voltage-c');
        if (outC) {
            if (outCount === 3) {
                outC.textContent = safeNumber(data.voltage_out_c, 0).toFixed(0) + 'V';
                if (outC.closest('.ups-badge')) outC.closest('.ups-badge').style.display = '';
            } else {
                if (outC.closest('.ups-badge')) outC.closest('.ups-badge').style.display = 'none';
            }
        }

        var outAmp = document.getElementById('ups-output-ampere');
        if (outAmp) outAmp.textContent = safeNumber(data.output_ampere, 0).toFixed(0);
        var outFreq = document.getElementById('ups-output-freq');
        if (outFreq) outFreq.textContent = safeNumber(data.output_freq, 0).toFixed(1);

        // Grounding Resistance
        var groundResistance = normalizeNullableNumber(data.grounding_resistance);
        var groundEl = document.getElementById('ups-ground-resistance');
        if (groundEl) {
            groundEl.textContent = groundResistance === null ? '--' : groundResistance.toFixed(1);
            groundEl.classList.toggle('ups-value-number--error', groundResistance === null);
        }
        updateGroundStatus(groundResistance);

        // Battery Info
        var rawBp = data.battery_percent;
        var hasBatteryPercent = rawBp !== null && rawBp !== undefined && rawBp !== "" && Number(rawBp) > 0;
        var bpVal = hasBatteryPercent ? Number(rawBp) : 0;

        updateBatteryGauge(bpVal, hasBatteryPercent);
        var batVolt = document.getElementById('ups-battery-voltage');
        if (batVolt) batVolt.textContent = safeNumber(data.battery_voltage, 0).toFixed(1) + ' V';
        var batRun = document.getElementById('ups-battery-runtime');
        if (batRun) batRun.textContent = safeNumber(data.battery_runtime_min, 0).toFixed(0) + ' min';
        var batStatus = document.getElementById('ups-battery-charge-status');
        if (batStatus) {
            var displayStatus = String(data.ups_status || '').replace(/\b\w/g, function (l) { return l.toUpperCase() });
            batStatus.textContent = displayStatus;

            var lowerStatus = String(data.ups_status || '').toLowerCase();
            var statusColor = '#4ade80'; // Default Green

            if (['off', 'output overload', 'offline'].includes(lowerStatus)) {
                statusColor = '#f87171'; // Red
            } else if (['on battery', 'on bypass', 'on boost', 'on maintenance bypass'].includes(lowerStatus)) {
                statusColor = '#fbbf24'; // Yellow
            } else if (['on', 'energy saver system', 'variable module management system'].includes(lowerStatus)) {
                statusColor = '#4ade80'; // Green
            }

            batStatus.style.color = statusColor;
        }

        // Ambient Temperature (UPS sensor) + Room Temperature
        updateTemperatureDisplay(
            data.ambient_temp,
            data.room_temp
        );

        // Wattage & Load
        var wattEl = document.getElementById('ups-total-watt');
        if (wattEl) wattEl.textContent = safeNumber(data.total_watts, 0).toFixed(0);
        var loadEl = document.getElementById('ups-load-percent');
        if (loadEl) loadEl.textContent = safeNumber(data.load_percent, 0).toFixed(0);

        applyCardGlassmorphismStates(data);
    }

    function applyCardGlassmorphismStates(data) {
        function setCardState(cardId, state) {
            var card = document.getElementById(cardId);
            if (!card) return;
            card.classList.remove('ups-glass-card--warning', 'ups-glass-card--critical');
            if (state === 'critical') card.classList.add('ups-glass-card--critical');
            else if (state === 'warning') card.classList.add('ups-glass-card--warning');
        }

        function toNum(v) {
            if (v === null || v === undefined || v === "") return null;
            var n = Number(v);
            return isNaN(n) ? null : n;
        }

        // 1. UPS Input Card
        var vInA = safeNumber(data.voltage_in_a, 0);
        var vInB = safeNumber(data.voltage_in_b, 0);
        var vInC = safeNumber(data.voltage_in_c, 0);
        var fIn = safeNumber(data.input_freq, 0);
        var inputState = 'normal';
        if ((vInA > 0 && (vInA < 190 || vInA > 250)) || (vInB > 0 && (vInB < 190 || vInB > 250)) || (vInC > 0 && (vInC < 190 || vInC > 250)) || (fIn > 0 && (fIn < 45 || fIn > 55))) {
            inputState = 'critical';
        } else if ((vInA > 0 && (vInA < 198 || vInA > 242)) || (vInB > 0 && (vInB < 198 || vInB > 242)) || (vInC > 0 && (vInC < 198 || vInC > 242)) || (fIn > 0 && (fIn < 47 || fIn > 53))) {
            inputState = 'warning';
        }
        setCardState('ups-card-ups-input', inputState);

        // 2. UPS Output Card
        var vOutA = safeNumber(data.voltage_out_a, 0);
        var vOutB = safeNumber(data.voltage_out_b, 0);
        var vOutC = safeNumber(data.voltage_out_c, 0);
        var fOut = safeNumber(data.output_freq, 0);
        var outputState = 'normal';
        if ((vInA > 0 && vOutA === 0) || (vOutA > 0 && (vOutA < 190 || vOutA > 250)) || (vOutB > 0 && (vOutB < 190 || vOutB > 250)) || (vOutC > 0 && (vOutC < 190 || vOutC > 250)) || (fOut > 0 && (fOut < 45 || fOut > 55))) {
            outputState = 'critical';
        } else if ((vOutA > 0 && (vOutA < 198 || vOutA > 242)) || (vOutB > 0 && (vOutB < 198 || vOutB > 242)) || (vOutC > 0 && (vOutC < 198 || vOutC > 242)) || (fOut > 0 && (fOut < 47 || fOut > 53))) {
            outputState = 'warning';
        }
        setCardState('ups-card-ups-output', outputState);

        // 3. Grounding Card
        var gr = normalizeNullableNumber(data.grounding_resistance);
        var groundState = 'normal';
        if (gr !== null && gr >= 5.0) groundState = 'critical';
        else if (gr !== null && gr >= 3.0) groundState = 'warning';
        setCardState('ups-card-ground', groundState);

        // 4. Battery Card
        var bp = toNum(data.battery_percent);
        var batState = 'normal';
        if (bp !== null && bp > 0 && bp <= 25) batState = 'critical';
        else if (bp !== null && bp > 0 && bp <= 40) batState = 'warning';
        setCardState('ups-card-battery', batState);

        // 5. Temperature Card
        var amb = toNum(data.ambient_temp);
        var room = toNum(data.room_temp);
        var tempState = 'normal';
        if ((amb !== null && amb >= 35.0) || (room !== null && room >= 35.0)) tempState = 'critical';
        else if ((amb !== null && amb >= 30.0) || (room !== null && room >= 30.0)) tempState = 'warning';
        setCardState('ups-card-temperature', tempState);

        // 6. Wattage & Load Card
        var load = safeNumber(data.load_percent, 0);
        var loadState = 'normal';
        if (load >= 90.0) loadState = 'critical';
        else if (load >= 80.0) loadState = 'warning';
        setCardState('ups-card-watt', loadState);
    }

    function fetchLatestUpsData(filters) {
        var query = new URLSearchParams();
        if (filters && filters.deviceId) query.set('device_id', filters.deviceId);
        if (filters && filters.siteId) query.set('site_id', filters.siteId);

        var url = '/api/ups/latest';
        var queryString = query.toString();
        if (queryString) url += '?' + queryString;

        return fetch(url, { credentials: 'same-origin', headers: getAuthHeaders() })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (payload) {
                if (!payload || !payload.data) return null;
                return normalizeUpsData(payload.data);
            })
            .catch(function () { return null; });
    }

    function fetchUpsHistory(deviceId) {
        if (!deviceId) {
            return Promise.resolve([]);
        }

        var url = '/api/ups/history?device_id=' + encodeURIComponent(deviceId);
        return fetch(url, { credentials: 'same-origin', headers: getAuthHeaders() })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (payload) {
                if (!payload || !payload.data) return [];
                return payload.data;
            })
            .catch(function () { return []; });
    }

    function toTimeLabel(value) {
        var date = new Date(value);
        if (Number.isNaN(date.getTime())) return String(value || '');
        var h = String(date.getHours()).padStart(2, '0');
        var m = String(date.getMinutes()).padStart(2, '0');
        return h + ':' + m;
    }

    function renderHistoryChart(rows) {
        if (!rows || rows.length === 0) {
            var fallbackLabels = generateTimeLabels();
            initMainChart(
                fallbackLabels,
                generateChartData(225, 20, fallbackLabels.length),
                generateChartData(222, 18, fallbackLabels.length)
            );
            return;
        }

        var sorted = rows.slice().sort(function (a, b) {
            return new Date(a.device_time) - new Date(b.device_time);
        });

        var bucketMs = 3 * 60 * 1000; // 3 minutes
        var firstTime = new Date(sorted[0].device_time).getTime();
        var lastTime = new Date(sorted[sorted.length - 1].device_time).getTime();

        var now = new Date().getTime();
        // If data is recent (within 30 mins), align chart end to now
        if (now - lastTime > bucketMs && now - lastTime <= 30 * 60 * 1000) {
            lastTime = now;
        }

        // Limit chart timeframe to max 24 hours to prevent memory issues if data is very old
        var maxTimeframeMs = 24 * 60 * 60 * 1000;
        if (lastTime - firstTime > maxTimeframeMs) {
            firstTime = lastTime - maxTimeframeMs;
        }

        // Ensure at least 30 minutes span on X axis so chart is legible
        var minTimeframeMs = 30 * 60 * 1000;
        if (lastTime - firstTime < minTimeframeMs) {
            firstTime = lastTime - minTimeframeMs;
        }

        var startTime = Math.floor(firstTime / bucketMs) * bucketMs;
        var endTime = Math.floor(lastTime / bucketMs) * bucketMs;

        var labels = [];
        var inputData = [];
        var outputData = [];

        var t = startTime;
        var rowIdx = 0;

        while (t <= endTime) {
            labels.push(toTimeLabel(t));

            var bucketEnd = t + bucketMs;
            var inAvg = 0;
            var outAvg = 0;
            var hasData = false;

            var sumIn = 0, sumOut = 0, count = 0;
            while (rowIdx < sorted.length) {
                var rowTime = new Date(sorted[rowIdx].device_time).getTime();
                if (rowTime < bucketEnd) {
                    if (rowTime >= t) {
                        var rawIn = sorted[rowIdx].voltage_in_avg != null
                            ? sorted[rowIdx].voltage_in_avg
                            : (sorted[rowIdx].voltage_in_a != null ? sorted[rowIdx].voltage_in_a : sorted[rowIdx].voltage_in);
                        var rawOut = sorted[rowIdx].voltage_out_avg != null
                            ? sorted[rowIdx].voltage_out_avg
                            : (sorted[rowIdx].voltage_out_a != null ? sorted[rowIdx].voltage_out_a : sorted[rowIdx].voltage_out);

                        sumIn += safeNumber(rawIn, 0);
                        sumOut += safeNumber(rawOut, 0);
                        count++;
                        hasData = true;
                    }
                    rowIdx++;
                } else {
                    break;
                }
            }

            if (hasData) {
                inAvg = Math.round((sumIn / count) * 10) / 10;
                outAvg = Math.round((sumOut / count) * 10) / 10;
            } else {
                inAvg = null;
                outAvg = null;
            }

            inputData.push(inAvg);
            outputData.push(outAvg);

            t += bucketMs;
        }

        initMainChart(labels, inputData, outputData);
    }

    function renderMonitoringDefaults() {
        renderUPSData(mockDbData);

        drawSparkline('sparkline-input', inputSparkData, '#818cf8', 'rgba(129, 140, 248, 0.2)');
        drawSparkline('sparkline-output', outputSparkData, '#f87171', 'rgba(248, 113, 113, 0.2)');
        drawSparkline('sparkline-ground', groundSparkData, '#a78bfa', 'rgba(167, 139, 250, 0.2)');
        drawSparkline('sparkline-temperature', tempSparkData, '#f87171', 'rgba(248, 113, 113, 0.2)');
        drawSparkline('sparkline-watt', wattSparkData, '#fbbf24', 'rgba(251, 191, 36, 0.2)');

        renderHistoryChart([]);
    }

    function setMonitoringState(isReady) {
        if (selectionPlaceholder) {
            selectionPlaceholder.classList.toggle('ups-hidden', isReady);
        }

        if (upsContent) {
            upsContent.classList.toggle('ups-hidden', !isReady);
        }

        if (!isReady) {
            monitoringDefaultsRendered = false;
            return;
        }

        if (monitoringDefaultsRendered) {
            return;
        }

        monitoringDefaultsRendered = true;
        renderMonitoringDefaults();
    }

    function setSelectOptions(selectEl, items, placeholder) {
        if (!selectEl) return;
        selectEl.innerHTML = '';
        var placeholderOption = document.createElement('option');
        placeholderOption.value = '';
        placeholderOption.textContent = placeholder;
        selectEl.appendChild(placeholderOption);

        items.forEach(function (item) {
            var option = document.createElement('option');
            option.value = item.value;
            option.textContent = item.label;
            selectEl.appendChild(option);
        });
    }

    function fetchSites() {
        return fetch('/api/ups/sites?only_with_ups=1', { credentials: 'same-origin', headers: getAuthHeaders() })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (payload) { return payload && payload.data ? payload.data : []; })
            .catch(function () { return []; });
    }

    function fetchDevices(siteId) {
        if (!siteId) return Promise.resolve([]);
        var url = '/api/ups/devices?site_id=' + encodeURIComponent(siteId);
        return fetch(url, { credentials: 'same-origin', headers: getAuthHeaders() })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (payload) { return payload && payload.data ? payload.data : []; })
            .catch(function () { return []; });
    }

    function formatPhaseTypeLabel(value) {
        var phase = (value || '').toString().trim();
        return phase || '3/3';
    }

    function buildDeviceOptionLabel(device) {
        var deviceName = device.device_name || device.name || '-';
        var upsType = device.ups_type || device.type || '-';
        var phaseType = formatPhaseTypeLabel(device.ups_phase_type || device.phase_type);
        var siteCode = device.site_code || device.code || '';

        var parts = [deviceName, upsType, phaseType];
        if (siteCode) parts.push(siteCode);
        return parts.join(' | ');
    }

    function loadUpsData(filters) {
        var cards = document.querySelectorAll('#ups-card-ups-input, #ups-card-ups-output, #ups-card-ground, #ups-card-battery, #ups-card-temperature, #ups-card-watt, .ups-chart-panel');
        cards.forEach(function (el) {
            el.classList.add('ups-shimmering');
        });

        var p1 = fetchLatestUpsData(filters).then(function (data) {
            if (data) {
                renderUPSData(data);
            } else {
                console.warn('No UPS data available. Device may not have recent logs.');
                // Still render with mock data for UI visibility, but log the issue
                renderUPSData(mockDbData);
            }
        });

        var p2 = fetchUpsHistory(filters.deviceId).then(function (rows) {
            renderHistoryChart(rows);
        });

        return Promise.all([p1, p2]).finally(function () {
            cards.forEach(function (el) {
                el.classList.remove('ups-shimmering');
            });
        });
    }

    function getCurrentUpsFilters() {
        var siteId = siteSelect ? siteSelect.value : '';
        var deviceId = deviceSelect ? deviceSelect.value : '';

        if (!siteId || !deviceId) {
            return null;
        }

        return {
            siteId: siteId,
            deviceId: deviceId
        };
    }

    function startUpsAutoRefresh() {
        if (autoRefreshTimer) {
            clearInterval(autoRefreshTimer);
        }

        if (window.UpsRefreshRing) {
            window.UpsRefreshRing.init(30, function () {
                var filters = getCurrentUpsFilters();
                if (!filters) return Promise.resolve();
                return loadUpsData(filters);
            });
        } else {
            autoRefreshTimer = setInterval(function () {
                var filters = getCurrentUpsFilters();
                if (!filters) {
                    return;
                }

                loadUpsData(filters);
            }, UPS_REFRESH_INTERVAL_MS);
        }
    }

    var siteSelect = document.getElementById('ups-site-select');
    var deviceSelect = document.getElementById('ups-device-select');
    setMonitoringState(false);
    startUpsAutoRefresh();

    fetchSites().then(function (sites) {
        var items = sites.map(function (site) {
            var siteId = site.id || site.site_id || '';
            var siteCode = site.site_code || site.code || '';
            var siteName = site.site_name || site.name || '';
            return {
                value: siteId,
                label: siteCode || siteName
            };
        });
        setSelectOptions(siteSelect, items, 'Pilih Site');

        var params = new URLSearchParams(window.location.search);
        var initialSite = params.get('site_id');
        var initialDevice = params.get('device_id');

        if (!initialSite && items.length > 0) {
            initialSite = items[0].value;
        }

        if (initialSite && siteSelect) {
            siteSelect.value = initialSite;
        }

        if (initialSite) {
            deviceSelect.disabled = true;
            fetchDevices(initialSite).then(function (devices) {
                var deviceItems = devices.map(function (device) {
                    var deviceId = device.id || device.device_id || '';
                    return {
                        value: deviceId,
                        label: buildDeviceOptionLabel(device)
                    };
                });
                setSelectOptions(deviceSelect, deviceItems, 'Pilih Device UPS');
                deviceSelect.disabled = false;

                if (!initialDevice && deviceItems.length > 0) {
                    initialDevice = deviceItems[0].value;
                }

                if (initialDevice) {
                    deviceSelect.value = initialDevice;
                    setMonitoringState(true);
                    loadUpsData({ siteId: initialSite, deviceId: initialDevice });
                } else {
                    setMonitoringState(false);
                }
            });
        } else {
            setMonitoringState(false);
        }
    });

    if (siteSelect) {
        siteSelect.addEventListener('change', function () {
            var siteId = siteSelect.value;
            deviceSelect.disabled = true;
            setSelectOptions(deviceSelect, [], 'Pilih Device UPS');

            if (!siteId) {
                setMonitoringState(false);
                return;
            }

            fetchDevices(siteId).then(function (devices) {
                var deviceItems = devices.map(function (device) {
                    var deviceId = device.id || device.device_id || '';
                    return {
                        value: deviceId,
                        label: buildDeviceOptionLabel(device)
                    };
                });
                setSelectOptions(deviceSelect, deviceItems, 'Pilih Device UPS');
                deviceSelect.disabled = false;
                if (deviceItems.length > 0) {
                    deviceSelect.value = deviceItems[0].value;
                    setMonitoringState(true);
                    loadUpsData({ siteId: siteId, deviceId: deviceItems[0].value });
                } else {
                    setMonitoringState(false);
                }
            });

            startUpsAutoRefresh();
        });
    }

    if (deviceSelect) {
        deviceSelect.addEventListener('change', function () {
            var siteId = siteSelect ? siteSelect.value : '';
            var deviceId = deviceSelect.value;
            if (!deviceId) {
                setMonitoringState(false);
                return;
            }
            setMonitoringState(true);
            loadUpsData({ siteId: siteId, deviceId: deviceId });
            startUpsAutoRefresh();
        });
    }

    // BATTERY UPDATE
    function updateBatteryGauge(percent, hasBatteryPercent) {
        var ringFill = document.getElementById('ups-battery-ring-fill');
        var batteryText = document.getElementById('ups-battery-percent');
        var statusBadge = document.getElementById('ups-battery-status-badge');
        var statusDot = document.getElementById('ups-battery-status-dot');

        if (!ringFill) return;

        var circumference = 326.7;

        if (!hasBatteryPercent) {
            ringFill.setAttribute('stroke-dashoffset', circumference * 0.25);
            ringFill.style.stroke = '#64748b'; // Neutral gray for N/A
            if (batteryText) batteryText.textContent = '--';
            if (statusBadge) {
                statusBadge.textContent = 'Normal';
                statusBadge.className = 'ups-status-badge ups-status-badge--normal';
            }
            if (statusDot) {
                statusDot.className = 'ups-status-dot ups-status-dot--normal';
            }
            var card = document.getElementById('ups-card-battery');
            if (card) {
                card.classList.remove('ups-glass-card--warning', 'ups-glass-card--critical');
            }
            return;
        }

        var offset = circumference - (percent / 100) * circumference;
        ringFill.setAttribute('stroke-dashoffset', offset);

        if (batteryText) batteryText.textContent = parseFloat(percent).toFixed(1);

        var color, statusText, statusClass;
        if (percent >= 60) {
            color = '#4ade80';
            statusText = 'Good';
            statusClass = 'normal';
        } else if (percent >= 30) {
            color = '#fbbf24';
            statusText = 'Low';
            statusClass = 'warning';
        } else {
            color = '#f87171';
            statusText = 'Critical';
            statusClass = 'critical';
        }

        ringFill.style.stroke = color;
        if (statusBadge) {
            statusBadge.textContent = statusText;
            statusBadge.className = 'ups-status-badge ups-status-badge--' + statusClass;
        }
        if (statusDot) {
            statusDot.className = 'ups-status-dot ups-status-dot--' + statusClass;
        }

        var card = document.getElementById('ups-card-battery');
        if (card) {
            card.classList.remove('ups-glass-card--normal', 'ups-glass-card--warning', 'ups-glass-card--critical', 'ups-glass-card--error');
            card.classList.add('ups-glass-card--' + statusClass);
        }
    }

    // TEMPERATURE UPDATE — Dual sensor (UPS machine + Room ambient)

    function getTempColor(temp) {
        if (temp < 30) return '#4ade80';
        if (temp < 35) return '#fbbf24';
        return '#f87171';
    }

    function getTempStatus(temp) {
        if (temp >= 35) return { class: 'critical', text: 'Critical' };
        if (temp >= 30) return { class: 'warning', text: 'Warning' };
        return { class: 'normal', text: 'Normal' };
    }

    function getTempBarPercent(temp) {
        var minTemp = 20, maxTemp = 35;
        return Math.min(Math.max((temp - minTemp) / (maxTemp - minTemp) * 100, 0), 100);
    }

    function updateTemperatureDisplay(ambientTemp, roomTemp) {
        ambientTemp = safeNumber(ambientTemp, 0);

        // Render Room Temperature
        var roomEl = document.getElementById('ups-room-temperature-value');
        if (roomEl) {
            if (roomTemp !== null && roomTemp !== undefined) {
                roomTemp = safeNumber(roomTemp, 0);
                roomEl.textContent = roomTemp.toFixed(1);
            } else {
                roomEl.textContent = '--';
            }
        }

        var roomTempBar = document.getElementById('ups-room-temp-bar-fill');
        var roomTempMarker = document.getElementById('ups-room-temp-bar-marker');
        var roomTempParent = document.getElementById('ups-room-temperature-value') ? document.getElementById('ups-room-temperature-value').closest('.ups-temp-item') : null;

        if (roomTemp !== null && roomTemp !== undefined && roomTemp !== '') {
            if (roomTempBar) {
                var roomPercent = getTempBarPercent(roomTemp);
                roomTempBar.style.width = roomPercent + '%';
                roomTempBar.style.backgroundColor = getTempColor(roomTemp);
            }
            if (roomTempMarker) {
                roomTempMarker.style.left = getTempBarPercent(roomTemp) + '%';
                roomTempMarker.style.display = 'block';
            }
            if (roomTempParent) {
                roomTempParent.style.opacity = '1';
            }
        } else {
            if (roomTempBar) {
                roomTempBar.style.width = '0%';
                roomTempBar.style.backgroundColor = 'transparent';
            }
            if (roomTempMarker) {
                roomTempMarker.style.display = 'none';
            }
            if (roomTempParent) {
                roomTempParent.style.opacity = '0.4';
            }
        }

        // Update UPS Machine temperature using ambient_temp from the database
        var ambientEl = document.getElementById('ups-temperature-value');
        if (ambientEl) {
            ambientEl.textContent = ambientTemp.toFixed(1);
        }

        var tempBar = document.getElementById('ups-temp-bar-fill');
        var tempMarker = document.getElementById('ups-temp-bar-marker');
        if (tempBar) {
            var percent = getTempBarPercent(ambientTemp);
            tempBar.style.width = percent + '%';
            tempBar.style.backgroundColor = getTempColor(ambientTemp);
        }

        if (tempMarker) {
            tempMarker.style.left = getTempBarPercent(ambientTemp) + '%';
        }

        // Update status badge
        var tempStatus = getTempStatus(ambientTemp);
        var statusBadge = document.getElementById('ups-temp-status-badge');
        if (statusBadge) {
            statusBadge.textContent = tempStatus.text;
            statusBadge.className = 'ups-status-badge ups-status-badge--' + tempStatus.class;
        }

        var statusDot = document.getElementById('ups-temp-status-dot');
        if (statusDot) {
            statusDot.className = 'ups-status-dot ups-status-dot--' + tempStatus.class;
        }
    }

    // Backward compat shim so existing callers still work
    function updateTemperatureBar(temp) {
        updateTemperatureDisplay(temp, null);
    }


    var resizeTimer;
    window.addEventListener('resize', function () {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(function () {
            if (!upsContent || upsContent.classList.contains('ups-hidden')) {
                return;
            }

            drawSparkline('sparkline-input', inputSparkData, '#818cf8', 'rgba(129, 140, 248, 0.2)');
            drawSparkline('sparkline-output', outputSparkData, '#f87171', 'rgba(248, 113, 113, 0.2)');
            drawSparkline('sparkline-ground', groundSparkData, '#a78bfa', 'rgba(167, 139, 250, 0.2)');
            drawSparkline('sparkline-temperature', tempSparkData, '#f87171', 'rgba(248, 113, 113, 0.2)');
            drawSparkline('sparkline-watt', wattSparkData, '#fbbf24', 'rgba(251, 191, 36, 0.2)');
        }, 200);
    });

    window.addEventListener('ups-theme-changed', function (e) {
        var theme = e.detail.theme;
        var isLight = theme === 'light';
        var themeBg = isLight ? '#f8fafc' : '#0a0e1a';

        if (mainChart) {
            mainChart.data.datasets[0].pointBorderColor = themeBg;
            mainChart.data.datasets[1].pointBorderColor = themeBg;

            mainChart.options.scales.x.grid.color = isLight ? 'rgba(148,163,184,0.12)' : 'rgba(255, 255, 255, 0.04)';
            mainChart.options.scales.x.ticks.color = isLight ? '#64748b' : '#475569';
            mainChart.options.scales.y.grid.color = isLight ? 'rgba(148,163,184,0.12)' : 'rgba(255, 255, 255, 0.04)';
            mainChart.options.scales.y.ticks.color = isLight ? '#64748b' : '#475569';

            mainChart.options.plugins.tooltip.backgroundColor = isLight ? 'rgba(255, 255, 255, 0.98)' : 'rgba(15, 23, 42, 0.95)';
            mainChart.options.plugins.tooltip.titleColor = isLight ? '#0f172a' : '#e2e8f0';
            mainChart.options.plugins.tooltip.bodyColor = isLight ? '#475569' : '#cbd5e1';
            mainChart.options.plugins.tooltip.borderColor = isLight ? 'rgba(148,163,184,0.22)' : 'rgba(139, 92, 246, 0.3)';

            mainChart.update();
        }

        // Redraw sparklines
        if (upsContent && !upsContent.classList.contains('ups-hidden')) {
            drawSparkline('sparkline-input', inputSparkData, '#818cf8', 'rgba(129, 140, 248, 0.2)');
            drawSparkline('sparkline-output', outputSparkData, '#f87171', 'rgba(248, 113, 113, 0.2)');
            drawSparkline('sparkline-ground', groundSparkData, '#a78bfa', 'rgba(167, 139, 250, 0.2)');
            drawSparkline('sparkline-temperature', tempSparkData, '#f87171', 'rgba(248, 113, 113, 0.2)');
            drawSparkline('sparkline-watt', wattSparkData, '#fbbf24', 'rgba(251, 191, 36, 0.2)');
        }
    });

});
