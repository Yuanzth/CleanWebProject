document.addEventListener('DOMContentLoaded', async function () {
    const urlParams = new URLSearchParams(window.location.search);
    const deviceId = urlParams.get('device_id');
    const targetDate = urlParams.get('date') || new Date().toISOString().split('T')[0];

    if (!deviceId) {
        alert("Device ID is missing.");
        window.close();
        return;
    }

    // Set input default
    const dateInput = document.getElementById('replayDateInput');
    if (dateInput) {
        dateInput.value = targetDate;
    }

    const btnChangeDate = document.getElementById('btnChangeDate');
    if (btnChangeDate) {
        btnChangeDate.addEventListener('click', () => {
            const newDate = dateInput.value;
            if (newDate && newDate !== targetDate) {
                // Change URL to fetch new date
                window.location.href = `/tracking/replay?device_id=${deviceId}&date=${newDate}`;
            }
        });
    }

    initMap();
    await loadData(deviceId, targetDate);
    initControls();
});

let map;
let trackData = null;
let polyline = null;
let dotLayer = null;
let segmentLayer = null;
let dotMarkers = [];
let segmentMarkers = [];
let stopMarkers = [];
let vehicleMarker = null;

let isPlaying = false;
let currentPointIndex = 0;
let playbackSpeed = 1; // 1x, 5x, 10x, 30x
let animationTimer = null;
let lastTimestamp = 0;

let speedChartInstance = null;
const RSRP_LINE_ZOOM_THRESHOLD = 13;

function initMap() {
    map = L.map('replayMap', {
        zoomControl: false,
        attributionControl: false
    }).setView([-2.5, 118], 5);

    L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
        maxZoom: 19,
    }).addTo(map);

    L.control.zoom({ position: 'topright' }).addTo(map);

    map.on('zoomend', applyReplayZoomVisualization);
}

function getRsrpColor(rsrp) {
    if (rsrp === null || rsrp === undefined || Number.isNaN(Number(rsrp))) {
        return '#94a3b8';
    }

    const val = Number(rsrp);
    if (val >= -85) return '#0000ff';
    if (val >= -100) return '#00ff00';
    if (val >= -105) return '#ffff00';
    if (val >= -115) return '#ff0000';
    return '#ff00ff';
}

function classifyRsrp(rsrp) {
    if (rsrp === null || rsrp === undefined || isNaN(Number(rsrp))) return 'unknown';
    const val = Number(rsrp);
    if (val >= -85) return 'excellent';
    if (val >= -100) return 'good';
    if (val >= -105) return 'fair';
    if (val >= -115) return 'poor';
    return 'nosignal';
}

function buildDotLayer(points) {
    if (!Array.isArray(points) || points.length === 0) return null;

    dotMarkers = points
        .map((p, idx) => ({
            p,
            idx
        }))
        .filter(item => Number.isFinite(Number(item.p.lat)) && Number.isFinite(Number(item.p.lng)))
        .map(item => {
            const marker = L.circleMarker([item.p.lat, item.p.lng], {
                radius: 7,
                weight: 2,
                color: '#ffffff',
                fillColor: getRsrpColor(item.p.network_rsrp),
                fillOpacity: 0,
                opacity: 0,
                className: 'rsrp-dot',
                rsrpLevel: classifyRsrp(item.p.network_rsrp)
            });
            marker.__dotIndex = item.idx;
            const timeStr = item.p.time ? new Date(item.p.time).toLocaleDateString('id-ID', {day:'2-digit', month:'short', year:'numeric'}) + ' ' + new Date(item.p.time).toLocaleTimeString('id-ID', {hour:'2-digit', minute:'2-digit', second:'2-digit'}) : '-';
            const rsrpVal = item.p.network_rsrp;
            const rsrpLabel = rsrpVal == null ? 'N/A' : rsrpVal >= -85 ? 'Excellent' : rsrpVal >= -100 ? 'Good' : rsrpVal >= -105 ? 'Fair' : rsrpVal >= -115 ? 'Poor' : 'No Signal';
            const rsrpColor = getRsrpColor(rsrpVal);
            marker.bindPopup(
                `<div style="font-size:12px;line-height:1.7;min-width:150px;">` +
                `<div style="display:flex;align-items:center;gap:6px;margin-bottom:4px;">` +
                `<span style="display:inline-block;width:10px;height:10px;border-radius:50%;background:${rsrpColor};border:1px solid rgba(0,0,0,0.15);"></span>` +
                `<strong style="color:#1e293b;font-size:13px;">Signal Info</strong>` +
                `<span style="margin-left:auto;font-size:10px;color:${rsrpColor};font-weight:600;">${rsrpLabel}</span>` +
                `</div>` +
                `<div style="color:#64748b;font-size:11px;margin-bottom:4px;padding-bottom:4px;border-bottom:1px solid #e2e8f0;">` +
                `<i class="fa-regular fa-clock"></i> ${timeStr}` +
                `${item.p.speed !== undefined ? ` &nbsp;|&nbsp; <i class="fa-solid fa-gauge"></i> ${item.p.speed} km/h` : ''}` +
                `</div>` +
                `RSSI: <b>${item.p.network_rssi ?? '-'}</b> dBm<br>` +
                `RSRP: <b>${item.p.network_rsrp ?? '-'}</b> dBm<br>` +
                `RSRQ: <b>${item.p.network_rsrq ?? '-'}</b> dB<br>` +
                `SINR: <b>${item.p.network_sinr ?? '-'}</b> dB</div>`,
                { offset: [0, -4], closeButton: true, autoClose: true }
            );
            return marker;
        });

    if (dotMarkers.length === 0) return null;
    return L.layerGroup(dotMarkers);
}

function buildSegmentLayer(points) {
    if (!Array.isArray(points) || points.length < 2) return null;

    segmentMarkers = [];
    for (let i = 1; i < points.length; i++) {
        const prev = points[i - 1];
        const curr = points[i];
        const lat1 = Number(prev.lat);
        const lng1 = Number(prev.lng);
        const lat2 = Number(curr.lat);
        const lng2 = Number(curr.lng);

        if (!Number.isFinite(lat1) || !Number.isFinite(lng1) || !Number.isFinite(lat2) || !Number.isFinite(lng2)) {
            continue;
        }

        const segment = L.polyline([[lat1, lng1], [lat2, lng2]], {
            color: getRsrpColor(curr.network_rsrp),
            weight: 4,
            opacity: 0,
            lineCap: 'round',
            lineJoin: 'round',
            interactive: false,
            className: 'rsrp-segment',
            rsrpLevel: classifyRsrp(curr.network_rsrp)
        });
        segment.__segmentIndex = i;
        segmentMarkers.push(segment);
    }

    if (segmentMarkers.length === 0) return null;
    return L.layerGroup(segmentMarkers);
}

function updateDotVisibility(currentIndex) {
    if (!dotMarkers || dotMarkers.length === 0) return;
    const maxIndex = Number.isFinite(currentIndex) ? currentIndex : -1;
    const filters = window.activeSignalFilters || { excellent: true, good: true, fair: true, poor: true, nosignal: true };

    dotMarkers.forEach(marker => {
        const level = marker.options.rsrpLevel || 'unknown';
        const isFilterActive = filters[level] !== false;
        const visible = marker.__dotIndex <= maxIndex && isFilterActive;
        marker.setStyle({
            opacity: visible ? 1 : 0,
            fillOpacity: visible ? 0.9 : 0
        });
    });
}

function updateSegmentVisibility(currentIndex) {
    if (!segmentMarkers || segmentMarkers.length === 0) return;
    const maxIndex = Number.isFinite(currentIndex) ? currentIndex : -1;
    const filters = window.activeSignalFilters || { excellent: true, good: true, fair: true, poor: true, nosignal: true };

    segmentMarkers.forEach(segment => {
        const level = segment.options.rsrpLevel || 'unknown';
        const isFilterActive = filters[level] !== false;
        const visible = segment.__segmentIndex <= maxIndex && isFilterActive;
        segment.setStyle({
            opacity: visible ? 0.9 : 0
        });
    });
}

function applyReplayZoomVisualization() {
    if (!map) return;

    const showLines = map.getZoom() <= RSRP_LINE_ZOOM_THRESHOLD;

    setDotLayerVisible(!showLines);
    setSegmentLayerVisible(showLines);
}

function setDotLayerVisible(visible) {
    if (!dotLayer) return;
    updateDotVisibility(currentPointIndex);
    if (!visible) {
        dotMarkers.forEach(marker => marker.setStyle({ opacity: 0, fillOpacity: 0 }));
    }
}

function setSegmentLayerVisible(visible) {
    if (!segmentLayer) return;
    updateSegmentVisibility(currentPointIndex);
    if (!visible) {
        segmentMarkers.forEach(segment => segment.setStyle({ opacity: 0 }));
    }
}

window.applySignalFilter = function() {
    if (!map) return;
    applyReplayZoomVisualization();
};

function getCsrfToken() {
    return document.querySelector('meta[name="csrf-token"]')?.content || '';
}

function sanitizeReplayRawPoints(points) {
    if (!Array.isArray(points)) return [];

    return points
        .map(p => ({
            ...p,
            lat: Number(p.lat),
            lng: Number(p.lng),
            speed: Number(p.speed) || 0,
            heading: Number.isFinite(Number(p.heading)) ? Number(p.heading) : null
        }))
        .filter(p => {
            if (!Number.isFinite(p.lat) || !Number.isFinite(p.lng)) return false;
            if (p.lat === 0 || p.lng === 0) return false;
            if (p.gps_status === 'Not Fixed') return false;
            return true;
        });
}

async function loadData(deviceId, targetDate) {
    try {
        const params = new URLSearchParams({
            date: targetDate, 
            time_start: '00:00:00',
            time_end: '23:59:59'
        });
        
        // We'll use the API token from meta if available, else session cookie should work
        const apiToken = document.querySelector('meta[name="api-token"]')?.content || '';
        const headers = {
            'Accept': 'application/json',
            'X-CSRF-TOKEN': getCsrfToken()
        };
        if (apiToken) {
            headers['Authorization'] = `Bearer ${apiToken}`;
        }

        const response = await fetch(`/api/tracking/unit/${deviceId}/history?${params.toString()}`, {
            headers: headers,
            credentials: 'include'
        });

        if (!response.ok) throw new Error("Failed to fetch data");
        const data = await response.json();

        if (data.status === 'success') {
            trackData = data;
            const filteredRawPoints = sanitizeReplayRawPoints(data.raw_points || []);
            trackData.raw_points = filteredRawPoints;
            
            document.getElementById('replayDeviceName').textContent = data.device.name || 'Unknown Device';

            if (filteredRawPoints.length > 0) {
                drawPath(filteredRawPoints);
                setupTimeline(filteredRawPoints);
                initChart(filteredRawPoints);
                updateVehiclePosition(0);
            } else {
                document.getElementById('replayDeviceName').textContent += " (No Data)";
            }
        } else {
            throw new Error(data.message || "Failed to load data");
        }
    } catch (e) {
        console.error(e);
        document.getElementById('replayDeviceName').textContent = "Error loading data";
    }
}

function drawPath(points) {
    const latlngs = points.map(p => [p.lat, p.lng]);

    if (dotLayer) {
        map.removeLayer(dotLayer);
        dotLayer = null;
    }
    if (segmentLayer) {
        map.removeLayer(segmentLayer);
        segmentLayer = null;
    }
    dotLayer = buildDotLayer(points);
    segmentLayer = buildSegmentLayer(points);
    if (segmentLayer) {
        segmentLayer.addTo(map);
    }
    if (dotLayer) {
        dotLayer.addTo(map);
    }

    updateDotVisibility(0);
    updateSegmentVisibility(0);
    applyReplayZoomVisualization();

    const bounds = L.latLngBounds(latlngs);
    map.fitBounds(bounds, { padding: [50, 50] });

    // Draw Stops (same logic as tracking.js)
    let currentStop = null;
    const stops = [];

    points.forEach(p => {
        if (p.speed === 0) {
            if (!currentStop) {
                currentStop = { lat: p.lat, lng: p.lng, startTime: new Date(p.time), endTime: new Date(p.time) };
            } else {
                currentStop.endTime = new Date(p.time);
            }
        } else {
            if (currentStop) {
                if ((currentStop.endTime - currentStop.startTime) >= 120000) stops.push(currentStop);
                currentStop = null;
            }
        }
    });
    if (currentStop && (currentStop.endTime - currentStop.startTime) >= 120000) {
        stops.push(currentStop);
    }

    stops.forEach((stop, index) => {
        const durationMins = Math.floor((stop.endTime - stop.startTime) / 60000);
        const stopIcon = L.divIcon({
            className: 'stop-marker-icon',
            html: `<div class="stop-marker-dot"><i class="fa-solid fa-square-parking"></i></div>`,
            iconSize: [26, 26],
            iconAnchor: [13, 13]
        });
        const marker = L.marker([stop.lat, stop.lng], { icon: stopIcon }).addTo(map);
        marker.bindTooltip(`
            <div style="text-align: center; line-height: 1.4;">
                <strong><i class="fa-solid fa-square-parking" style="color:#ef4444;margin-right:4px;"></i> Idle / Stop #${index + 1}</strong><br>
                Durasi: ${durationMins} mnt<br>
            </div>
        `, { direction: 'top', offset: [0, -10] });
        stopMarkers.push(marker);
    });
}

function setupTimeline(points) {
    const slider = document.getElementById('replayTimeline');
    slider.min = 0;
    slider.max = points.length - 1;
    slider.value = 0;

    const startTimeStr = new Date(points[0].time).toLocaleTimeString('id-ID', {hour: '2-digit', minute:'2-digit'});
    const endTimeStr = new Date(points[points.length - 1].time).toLocaleTimeString('id-ID', {hour: '2-digit', minute:'2-digit'});

    document.getElementById('replayStartTime').textContent = startTimeStr;
    document.getElementById('replayEndTime').textContent = endTimeStr;

    slider.addEventListener('input', (e) => {
        const val = parseInt(e.target.value);
        currentPointIndex = val;
        updateVehiclePosition(val);
    });
}

function updateVehiclePosition(index) {
    if (!trackData || !trackData.raw_points) return;
    const point = trackData.raw_points[index];
    if (!point) return;

    // Convert heading from API to Leaflet rotation (same as tracking.js)
    let headingDeg = point.heading || 0;
    let rotation = (headingDeg + 180) % 360;

    if (!vehicleMarker) {
        const status = point.speed === 0 ? 'idle' : 'on';
        const fleetType = trackData.device ? trackData.device.fleet_type : 'Heavy Duty Truck';
        const iconUrl = getUnitIcon(status, fleetType); 
        const icon = L.divIcon({
            className: 'replay-marker-icon',
            html: `
                <div class="unit-marker-wrapper">
                    <img src="${iconUrl}" data-current-rotation="${rotation}" style="transform: rotate(${rotation}deg); width: 52px; height: 52px;">
                </div>
            `,
            iconSize: [52, 52],
            iconAnchor: [26, 26],
        });
        vehicleMarker = L.marker([point.lat, point.lng], { icon: icon, zIndexOffset: 1000 }).addTo(map);
    } else {
        vehicleMarker.setLatLng([point.lat, point.lng]);
        const img = vehicleMarker._icon.querySelector('img');
        if (img) {
            // Smooth rotation logic
            let currentRotation = parseFloat(img.dataset.currentRotation) || 0;
            let targetRotation = rotation;
            let diff = (targetRotation - currentRotation) % 360;
            if (diff > 180) diff -= 360;
            if (diff < -180) diff += 360;
            const finalRotation = currentRotation + diff;
            
            img.dataset.currentRotation = finalRotation;
            img.style.transform = `rotate(${finalRotation}deg)`;
            
            // Color based on speed
            const status = point.speed === 0 ? 'idle' : 'on';
            const fleetType = trackData.device ? trackData.device.fleet_type : 'Heavy Duty Truck';
            const iconUrl = getUnitIcon(status, fleetType);
            img.src = iconUrl;
        }
    }

    // Update controls text
    const dateObj = new Date(point.time);
    document.getElementById('replayCurrentTime').textContent = dateObj.toLocaleTimeString('id-ID', {hour: '2-digit', minute:'2-digit', second:'2-digit'});
    document.getElementById('replayCurrentSpeed').textContent = `${point.speed} km/h`;
    document.getElementById('replayTimeline').value = index;

    updateDotVisibility(index);
    updateSegmentVisibility(index);
    applyReplayZoomVisualization();
    
    // Auto-pan map if it gets close to edge
    if (isPlaying && !map.getBounds().contains([point.lat, point.lng])) {
        map.panTo([point.lat, point.lng]);
    }
}

function initControls() {
    const playBtn = document.getElementById('btnPlayPause');
    
    playBtn.addEventListener('click', () => {
        if (!trackData || !trackData.raw_points) return;
        
        isPlaying = !isPlaying;
        updatePlayBtnState();
        
        if (isPlaying) {
            // If at the end, restart
            if (currentPointIndex >= trackData.raw_points.length - 1) {
                currentPointIndex = 0;
                lerpProgress = 0;
            }
            lastTimestamp = performance.now();
            animationTimer = requestAnimationFrame(animate);
        } else {
            cancelAnimationFrame(animationTimer);
        }
    });

    // Speed selector
    const speedBtns = document.querySelectorAll('.speed-btn');
    speedBtns.forEach(btn => {
        btn.addEventListener('click', (e) => {
            speedBtns.forEach(b => b.classList.remove('active'));
            e.target.classList.add('active');
            playbackSpeed = parseFloat(e.target.dataset.speed);
        });
    });
}

function updatePlayBtnState() {
    const icon = document.querySelector('#btnPlayPause i');
    if (isPlaying) {
        icon.className = 'fa-solid fa-pause';
    } else {
        icon.className = 'fa-solid fa-play';
    }
}

// Smooth interpolation progress between two consecutive points
let lerpProgress = 0;

function animate(timestamp) {
    if (!isPlaying) return;

    const delta = timestamp - lastTimestamp;
    lastTimestamp = timestamp;

    // Base speed: 1x = 2 points per second (much slower & smoother)
    const pointsPerSecond = 2 * playbackSpeed;
    const progressPerMs = pointsPerSecond / 1000;

    // Advance lerp progress
    lerpProgress += delta * progressPerMs;

    while (lerpProgress >= 1) {
        // Move to next point
        lerpProgress -= 1;
        currentPointIndex++;

        if (currentPointIndex >= trackData.raw_points.length - 1) {
            // Reached the end
            currentPointIndex = trackData.raw_points.length - 1;
            lerpProgress = 0;
            updateVehiclePosition(currentPointIndex);
            isPlaying = false;
            updatePlayBtnState();
            return;
        }
    }

    // Interpolate between currentPointIndex and currentPointIndex+1
    const pointA = trackData.raw_points[currentPointIndex];
    const pointB = trackData.raw_points[Math.min(currentPointIndex + 1, trackData.raw_points.length - 1)];

    if (pointA && pointB && vehicleMarker) {
        // Lerp position
        const lat = pointA.lat + (pointB.lat - pointA.lat) * lerpProgress;
        const lng = pointA.lng + (pointB.lng - pointA.lng) * lerpProgress;
        vehicleMarker.setLatLng([lat, lng]);

        // Lerp rotation (shortest path)
        const headingA = ((pointA.heading || 0) + 180) % 360;
        const headingB = ((pointB.heading || 0) + 180) % 360;
        let rotDiff = (headingB - headingA) % 360;
        if (rotDiff > 180) rotDiff -= 360;
        if (rotDiff < -180) rotDiff += 360;
        const interpolatedRotation = headingA + rotDiff * lerpProgress;

        const img = vehicleMarker._icon?.querySelector('img');
        if (img) {
            img.dataset.currentRotation = interpolatedRotation;
            img.style.transform = `rotate(${interpolatedRotation}deg)`;

            // Update icon color based on current point speed
            const status = pointA.speed === 0 ? 'idle' : 'on';
            const fleetType = trackData.device ? trackData.device.fleet_type : 'Heavy Duty Truck';
            img.src = getUnitIcon(status, fleetType);
        }

        // Auto-pan map
        if (!map.getBounds().contains([lat, lng])) {
            map.panTo([lat, lng]);
        }
    } else if (pointA) {
        // First frame or no vehicleMarker yet
        updateVehiclePosition(currentPointIndex);
    }

    // Update UI controls (time, speed, slider, dots) at discrete point level
    const displayPoint = trackData.raw_points[currentPointIndex];
    if (displayPoint) {
        const dateObj = new Date(displayPoint.time);
        document.getElementById('replayCurrentTime').textContent = dateObj.toLocaleTimeString('id-ID', {hour: '2-digit', minute:'2-digit', second:'2-digit'});
        document.getElementById('replayCurrentSpeed').textContent = `${displayPoint.speed} km/h`;
        document.getElementById('replayTimeline').value = currentPointIndex;

        updateDotVisibility(currentPointIndex);
        updateSegmentVisibility(currentPointIndex);
        applyReplayZoomVisualization();
    }

    animationTimer = requestAnimationFrame(animate);
}

function initChart(points) {
    const canvas = document.getElementById('replaySpeedChart');
    if (!canvas) return;
    
    const ctx = canvas.getContext('2d');
    
    // Create data arrays
    const labels = [];
    const data = [];
    
    points.forEach(p => {
        const d = new Date(p.time);
        labels.push(d.toLocaleTimeString('id-ID', { hour: '2-digit', minute: '2-digit' }));
        data.push(p.speed);
    });

    if (speedChartInstance) {
        speedChartInstance.destroy();
    }

    speedChartInstance = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: 'Km/h',
                data: data,
                borderColor: '#D4952B',
                backgroundColor: 'rgba(212, 149, 43, 0.1)',
                borderWidth: 1.5,
                fill: true,
                tension: 0.2,
                pointRadius: 0,
                pointHoverRadius: 5,
                pointHoverBackgroundColor: '#D4952B',
                pointHoverBorderColor: '#fff',
                pointHoverBorderWidth: 2,
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: 'rgba(30, 41, 59, 0.9)',
                    titleFont: { size: 10 },
                    bodyFont: { size: 11 },
                    padding: 8,
                    cornerRadius: 6,
                    callbacks: {
                        title: (items) => items[0].label,
                        label: (item) => `${item.raw} Km/h`
                    }
                }
            },
            scales: {
                x: {
                    display: true,
                    ticks: { font: { size: 9 }, color: '#94a3b8', maxTicksLimit: 12, maxRotation: 0 },
                    grid: { display: false },
                    border: { display: false },
                },
                y: {
                    display: true,
                    ticks: { font: { size: 9 }, color: '#94a3b8', maxTicksLimit: 3 },
                    grid: { color: 'rgba(255,255,255,0.05)', drawBorder: false },
                    border: { display: false },
                    beginAtZero: true,
                }
            },
            interaction: {
                intersect: false,
                mode: 'index',
            },
            onHover: (event, elements) => {
                if (elements.length > 0) {
                    const idx = elements[0].index;
                    // Auto seek timeline to hovered point if not playing
                    if (!isPlaying) {
                        const slider = document.getElementById('replayTimeline');
                        slider.value = idx;
                        currentPointIndex = idx;
                        updateVehiclePosition(idx);
                    }
                }
            },
        }
    });
}

function getUnitIcon(status, fleet_type = "Heavy Duty Truck") {
    let type = fleet_type ? fleet_type.replace(/\s+/g, '') : "HeavyDutyTruck";

    if (type == "HeavyDutyTruck" || type == "HeavyDuty") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/HeavyDutyTruck/HD_Green.png';
            case 'off': return '/assets/img/Tracking/HeavyDutyTruck/HD_Red.png';
            case 'idle': return '/assets/img/Tracking/HeavyDutyTruck/HD_Yellow.png';
            default: return '/assets/img/Tracking/HeavyDutyTruck/HD_Green.png';
        }
    }
    else if (type == "LightVehicle") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/LightVehicle/LV_Green.png';
            case 'off': return '/assets/img/Tracking/LightVehicle/LV_Red.png';
            case 'idle': return '/assets/img/Tracking/LightVehicle/LV_Yellow.png';
            default: return '/assets/img/Tracking/LightVehicle/LV_Green.png';
        }
    }
    else if (type == "ArticulatedDumpTruck") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/ArticulatedDumpTruck/ADT_Green.png';
            case 'off': return '/assets/img/Tracking/ArticulatedDumpTruck/ADT_Red.png';
            case 'idle': return '/assets/img/Tracking/ArticulatedDumpTruck/ADT_Yellow.png';
            default: return '/assets/img/Tracking/ArticulatedDumpTruck/ADT_Green.png';
        }
    }
    else if (type == "FuelTruck" || type == "WaterTruck") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/Fuel&WaterTruck/FT_Green.png';
            case 'off': return '/assets/img/Tracking/Fuel&WaterTruck/FT_Red.png';
            case 'idle': return '/assets/img/Tracking/Fuel&WaterTruck/FT_Yellow.png';
            default: return '/assets/img/Tracking/Fuel&WaterTruck/FT_Green.png';
        }
    }
    else if (type == "DumpTrailer" || type == "DumpTrailerTruck") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/DumpTrailer/DV_Green.png';
            case 'off': return '/assets/img/Tracking/DumpTrailer/DV_Red.png';
            case 'idle': return '/assets/img/Tracking/DumpTrailer/DV_Yellow.png';
            default: return '/assets/img/Tracking/DumpTrailer/DV_Green.png';
        }
    }
    else if (type == "Grader" || type == "Roller") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/Grader/GRD_Green.png';
            case 'off': return '/assets/img/Tracking/Grader/GRD_Red.png';
            case 'idle': return '/assets/img/Tracking/Grader/GRD_Yellow.png';
            default: return '/assets/img/Tracking/Grader/GRD_Green.png';
        }
    }
    else if (type == "Excavator") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/Excavator/PC_Green.png';
            case 'off': return '/assets/img/Tracking/Excavator/PC_Red.png';
            case 'idle': return '/assets/img/Tracking/Excavator/PC_Yellow.png';
            default: return '/assets/img/Tracking/Excavator/PC_Green.png';
        }
    }
    return '/assets/img/Tracking/HeavyDutyTruck/HD_Green.png';
}
