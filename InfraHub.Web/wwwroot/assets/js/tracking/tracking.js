// Data dari API (akan di-load saat page load)
let SITES_DATA = {}; // Will be populated from API

// Track points cache untuk setiap device
const TRACK_CACHE = {}; // { [device_id]: { points: [...], summary: {...} } }

// API Base URL
const API_BASE = '/Tracking/Api';

// =============================================
// DUMMY DATA: 7 Units with traces for front-end
// =============================================
const DUMMY_UNITS_DATA = [
    {
        id: 'DUMMY-HD-001',
        name: 'HD-201',
        fleet_type: 'Heavy Duty Truck',
        status: 'on',
        speed: 35,
        gps: 'Excellent',
        satellite: 12,
        heading: 145,
        lat: -1.6200,
        lng: 116.0600,
        lastUpdate: '2026-06-03 08:30:00',
        networkQuality: { rssi: -55, rsrp: -78, rsrq: -6, sinr: 22 },
        signalProfile: 'excellent' // Mostly excellent signal
    },
    {
        id: 'DUMMY-HD-002',
        name: 'HD-202',
        fleet_type: 'Heavy Duty Truck',
        status: 'on',
        speed: 22,
        gps: 'Excellent',
        satellite: 10,
        heading: 270,
        lat: -1.6250,
        lng: 116.0550,
        lastUpdate: '2026-06-03 08:28:00',
        networkQuality: { rssi: -68, rsrp: -92, rsrq: -9, sinr: 12 },
        signalProfile: 'good' // Mostly good signal
    },
    {
        id: 'DUMMY-EX-001',
        name: 'EX-301',
        fleet_type: 'Excavator',
        status: 'idle',
        speed: 0,
        gps: 'Good',
        satellite: 8,
        heading: 90,
        lat: -1.6180,
        lng: 116.0680,
        lastUpdate: '2026-06-03 08:25:00',
        networkQuality: { rssi: -82, rsrp: -102, rsrq: -13, sinr: 4 },
        signalProfile: 'fair' // Fair signal area
    },
    {
        id: 'DUMMY-ADT-001',
        name: 'ADT-401',
        fleet_type: 'Articulated Dump Truck',
        status: 'on',
        speed: 28,
        gps: 'Excellent',
        satellite: 11,
        heading: 315,
        lat: -1.6300,
        lng: 116.0500,
        lastUpdate: '2026-06-03 08:32:00',
        networkQuality: { rssi: -90, rsrp: -108, rsrq: -16, sinr: 1 },
        signalProfile: 'poor' // Poor signal area
    },
    {
        id: 'DUMMY-LV-001',
        name: 'LV-101',
        fleet_type: 'Light Vehicle',
        status: 'on',
        speed: 45,
        gps: 'Excellent',
        satellite: 14,
        heading: 60,
        lat: -1.6150,
        lng: 116.0700,
        lastUpdate: '2026-06-03 08:35:00',
        networkQuality: { rssi: -52, rsrp: -75, rsrq: -5, sinr: 25 },
        signalProfile: 'excellent' // Excellent signal
    },
    {
        id: 'DUMMY-GRD-001',
        name: 'GRD-501',
        fleet_type: 'Grader',
        status: 'idle',
        speed: 0,
        gps: 'Good',
        satellite: 7,
        heading: 180,
        lat: -1.6280,
        lng: 116.0620,
        lastUpdate: '2026-06-03 08:20:00',
        networkQuality: { rssi: -95, rsrp: -112, rsrq: -17, sinr: -1 },
        signalProfile: 'poor' // Poor/no signal area
    },
    {
        id: 'DUMMY-FT-001',
        name: 'FT-601',
        fleet_type: 'Fuel Truck',
        status: 'off',
        speed: 0,
        gps: 'Poor',
        satellite: 4,
        heading: 200,
        lat: -1.6350,
        lng: 116.0450,
        lastUpdate: '2026-06-03 07:45:00',
        networkQuality: { rssi: -105, rsrp: -120, rsrq: -20, sinr: -6 },
        signalProfile: 'nosignal' // Very weak/no signal
    }
];

/**
 * Generate dummy trace (raw_points) for a unit based on signal profile
 * Creates realistic GPS trail with signal strength data
 */
function generateDummyTraceForUnit(unit, pointCount = 80) {
    const points = [];
    const rawPoints = [];
    const now = new Date();
    const startLat = unit.lat + 0.012;
    const startLng = unit.lng - 0.010;

    for (let i = 0; i <= pointCount; i++) {
        const progress = i / pointCount;
        const t = new Date(now.getTime() - (pointCount - i) * 60000); // 1 point per minute

        // Create a curved path
        const curve = Math.sin(progress * Math.PI * 1.5) * 0.003;
        const lat = startLat + (unit.lat - startLat) * progress + curve;
        const lng = startLng + (unit.lng - startLng) * progress - curve * 0.5;

        // Add small jitter for realism
        const jLat = (Math.random() - 0.5) * 0.0002;
        const jLng = (Math.random() - 0.5) * 0.0002;
        const finalLat = lat + jLat;
        const finalLng = lng + jLng;

        points.push([finalLat, finalLng]);

        // Generate signal values based on unit's signal profile
        let rsrp, rsrq, sinr, rssi;
        const rand = Math.random();

        switch (unit.signalProfile) {
            case 'excellent':
                if (rand < 0.65) { rsrp = -70 - Math.random() * 14; rsrq = -4 - Math.random() * 3; sinr = 18 + Math.random() * 12; rssi = -48 - Math.random() * 12; }
                else if (rand < 0.85) { rsrp = -86 - Math.random() * 13; rsrq = -7 - Math.random() * 4; sinr = 10 + Math.random() * 7; rssi = -62 - Math.random() * 12; }
                else { rsrp = -100 - Math.random() * 4; rsrq = -11 - Math.random() * 3; sinr = 4 + Math.random() * 5; rssi = -78 - Math.random() * 8; }
                break;
            case 'good':
                if (rand < 0.25) { rsrp = -72 - Math.random() * 12; rsrq = -5 - Math.random() * 3; sinr = 16 + Math.random() * 10; rssi = -50 - Math.random() * 12; }
                else if (rand < 0.70) { rsrp = -86 - Math.random() * 13; rsrq = -8 - Math.random() * 4; sinr = 9 + Math.random() * 6; rssi = -64 - Math.random() * 14; }
                else if (rand < 0.90) { rsrp = -100 - Math.random() * 4; rsrq = -12 - Math.random() * 3; sinr = 3 + Math.random() * 5; rssi = -80 - Math.random() * 8; }
                else { rsrp = -106 - Math.random() * 8; rsrq = -15 - Math.random() * 4; sinr = -1 + Math.random() * 4; rssi = -88 - Math.random() * 8; }
                break;
            case 'fair':
                if (rand < 0.15) { rsrp = -74 - Math.random() * 10; rsrq = -6 - Math.random() * 3; sinr = 14 + Math.random() * 8; rssi = -55 - Math.random() * 10; }
                else if (rand < 0.35) { rsrp = -88 - Math.random() * 11; rsrq = -9 - Math.random() * 4; sinr = 7 + Math.random() * 6; rssi = -68 - Math.random() * 10; }
                else if (rand < 0.75) { rsrp = -100 - Math.random() * 4.5; rsrq = -12 - Math.random() * 3; sinr = 2 + Math.random() * 5; rssi = -82 - Math.random() * 8; }
                else { rsrp = -106 - Math.random() * 9; rsrq = -15 - Math.random() * 5; sinr = -2 + Math.random() * 4; rssi = -92 - Math.random() * 8; }
                break;
            case 'poor':
                if (rand < 0.10) { rsrp = -80 - Math.random() * 5; rsrq = -8 - Math.random() * 3; sinr = 10 + Math.random() * 5; rssi = -62 - Math.random() * 8; }
                else if (rand < 0.25) { rsrp = -90 - Math.random() * 9; rsrq = -10 - Math.random() * 4; sinr = 5 + Math.random() * 5; rssi = -72 - Math.random() * 10; }
                else if (rand < 0.55) { rsrp = -100 - Math.random() * 4; rsrq = -13 - Math.random() * 3; sinr = 1 + Math.random() * 4; rssi = -84 - Math.random() * 6; }
                else if (rand < 0.85) { rsrp = -106 - Math.random() * 8; rsrq = -16 - Math.random() * 4; sinr = -2 + Math.random() * 4; rssi = -92 - Math.random() * 8; }
                else { rsrp = -116 - Math.random() * 8; rsrq = -19 - Math.random() * 3; sinr = -5 - Math.random() * 4; rssi = -100 - Math.random() * 8; }
                break;
            case 'nosignal':
                if (rand < 0.05) { rsrp = -86 - Math.random() * 13; rsrq = -9 - Math.random() * 4; sinr = 6 + Math.random() * 6; rssi = -68 - Math.random() * 10; }
                else if (rand < 0.15) { rsrp = -101 - Math.random() * 4; rsrq = -13 - Math.random() * 3; sinr = 1 + Math.random() * 4; rssi = -84 - Math.random() * 6; }
                else if (rand < 0.35) { rsrp = -106 - Math.random() * 8; rsrq = -16 - Math.random() * 4; sinr = -3 + Math.random() * 5; rssi = -94 - Math.random() * 6; }
                else { rsrp = -116 - Math.random() * 12; rsrq = -19 - Math.random() * 4; sinr = -6 - Math.random() * 5; rssi = -102 - Math.random() * 10; }
                break;
            default:
                rsrp = -90 - Math.random() * 20; rsrq = -10 - Math.random() * 8; sinr = 5 + Math.random() * 10; rssi = -70 - Math.random() * 20;
        }

        // Speed: varies, with some idle stops
        let speed = 0;
        if (unit.status === 'off') {
            speed = 0;
        } else if (unit.status === 'idle') {
            speed = (rand < 0.7) ? 0 : Math.round(Math.random() * 8);
        } else {
            speed = (rand < 0.15) ? 0 : Math.round(5 + Math.random() * 45);
        }

        rawPoints.push({
            lat: finalLat,
            lng: finalLng,
            network_rssi: Math.round(rssi * 10) / 10,
            network_rsrp: Math.round(rsrp * 10) / 10,
            network_rsrq: Math.round(rsrq * 10) / 10,
            network_sinr: Math.round(sinr * 10) / 10,
            speed: speed,
            time: t.toISOString(),
            gps_status: 'Fixed'
        });
    }

    return { points, rawPoints };
}

/**
 * Pre-generate and cache dummy trace data for all 7 units
 */
function initDummyTraceCaches() {
    DUMMY_UNITS_DATA.forEach(unit => {
        const traceData = generateDummyTraceForUnit(unit);
        TRACK_CACHE[unit.id] = {
            points: traceData.points,
            raw_points: traceData.rawPoints,
            summary: {},
            device: { id: unit.id, name: unit.name },
            totalPoints: traceData.points.length
        };
    });
    console.log(`✓ Dummy trace data cached for ${DUMMY_UNITS_DATA.length} units`);
}

// Date range state
let selectedDateRange = { start: null, end: null };

/**
 * Helper to get CSRF token from meta tag
 */
function getCsrfToken() {
    return document.querySelector('meta[name="csrf-token"]')?.content || '';
}

/**
 * Helper to make authenticated API calls
 */
async function apiCall(url, options = {}) {
    const csrfToken = getCsrfToken();

    if (!csrfToken) {
        console.warn('CSRF token not found, some POST requests might fail');
    }

    const apiToken = document.querySelector('meta[name="api-token"]')?.content || '';
    const headers = {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': csrfToken,
        'Accept': 'application/json',
        ...options.headers
    };

    if (apiToken && apiToken.trim() !== '') {
        headers['Authorization'] = `Bearer ${apiToken}`;
    }

    const response = await fetch(url, {
        method: options.method || 'GET',
        headers: headers,
        credentials: 'include',  // Include session cookies
        ...options
    });

    if (!response.ok) {
        const text = await response.text();
        console.error(`API Error [${response.status}]:`, text.substring(0, 500));
        throw new Error(`API Error: ${response.status} ${response.statusText}`);
    }

    return await response.json();
}

function normalizeFleetType(fleetType) {
    if (fleetType === null || fleetType === undefined) return 'heavydutytruck';
    return String(fleetType)
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]/g, '');
}

function getUnitIcon(status, fleet_type = "Heavy Duty Truck") {
    const type = normalizeFleetType(fleet_type);

    if (type === "heavydutytruck" || type === "heavyduty" || type === "hd") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/HeavyDutyTruck/HD_Green.png';
            case 'off': return '/assets/img/Tracking/HeavyDutyTruck/HD_Red.png';
            case 'idle': return '/assets/img/Tracking/HeavyDutyTruck/HD_Yellow.png';
            default: return '/assets/img/Tracking/HeavyDutyTruck/HD_Green.png';
        }
    }
    else if (type === "lightvehicle" || type === "lv") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/LightVehicle/LV_Green.png';
            case 'off': return '/assets/img/Tracking/LightVehicle/LV_Red.png';
            case 'idle': return '/assets/img/Tracking/LightVehicle/LV_Yellow.png';
            default: return '/assets/img/Tracking/LightVehicle/LV_Green.png';
        }
    }
    else if (type === "articulateddumptruck" || type === "adt") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/ArticulatedDumpTruck/ADT_Green.png';
            case 'off': return '/assets/img/Tracking/ArticulatedDumpTruck/ADT_Red.png';
            case 'idle': return '/assets/img/Tracking/ArticulatedDumpTruck/ADT_Yellow.png';
            default: return '/assets/img/Tracking/ArticulatedDumpTruck/ADT_Green.png';
        }
    }
    else if (type === "fueltruck" || type === "watertruck") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/Fuel&WaterTruck/FT_Green.png';
            case 'off': return '/assets/img/Tracking/Fuel&WaterTruck/FT_Red.png';
            case 'idle': return '/assets/img/Tracking/Fuel&WaterTruck/FT_Yellow.png';
            default: return '/assets/img/Tracking/Fuel&WaterTruck/FT_Green.png';
        }
    }
    else if (type === "dumptrailer" || type === "dumptrailertruck") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/DumpTrailer/DV_Green.png';
            case 'off': return '/assets/img/Tracking/DumpTrailer/DV_Red.png';
            case 'idle': return '/assets/img/Tracking/DumpTrailer/DV_Yellow.png';
            default: return '/assets/img/Tracking/DumpTrailer/DV_Green.png';
        }
    }
    else if (type === "grader" || type === "roller") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/Grader/GRD_Green.png';
            case 'off': return '/assets/img/Tracking/Grader/GRD_Red.png';
            case 'idle': return '/assets/img/Tracking/Grader/GRD_Yellow.png';
            default: return '/assets/img/Tracking/Grader/GRD_Green.png';
        }
    }
    else if (type === "excavator") {
        switch (status) {
            case 'on': return '/assets/img/Tracking/Excavator/PC_Green.png';
            case 'off': return '/assets/img/Tracking/Excavator/PC_Red.png';
            case 'idle': return '/assets/img/Tracking/Excavator/PC_Yellow.png';
            default: return '/assets/img/Tracking/Excavator/PC_Green.png';
        }
    }
    return '/assets/img/Tracking/HeavyDutyTruck/HD_Green.png';
}

/**
 * Compute a display status for a unit based on API status and speed.
 * Priority:
 * - 'off' from API stays 'off'
 * - if speed === 0 => 'idle'
 * - otherwise 'on'
 */
function computeDisplayStatus(unitLike) {
    if (!unitLike) return 'off';
    // If API explicitly marks off, respect it
    if (unitLike.status === 'off') return 'off';
    // Try common speed fields
    const speed = Number(unitLike.speed ?? unitLike.speed_kmh ?? 0);
    if (!Number.isFinite(speed)) return unitLike.status || 'off';
    return speed === 0 ? 'idle' : 'on';
}

// =============================================
// API Functions
// =============================================

/**
 * Load sites dengan units dari API
 * Uses session auth with CSRF token (appropriate for Blade templates)
 */
async function loadSitesFromAPI() {
    try {
        const data = await apiCall(`${API_BASE}/GetSitesWithUnits`);

        if (data.status === 'success' && data.data) {
            // Transform API response to hierarchical format for frontend
            SITES_DATA = transformSitesData(data.data);
            console.log('Sites loaded successfully:', Object.values(SITES_DATA).reduce((total, area) => total + area.sites.length, 0), 'sites');
        } else {
            console.error('Invalid API response format:', data);
            SITES_DATA = {};
        }
    } catch (error) {
        console.error('Error loading sites from API:', error);
        SITES_DATA = {};
    }
}

/**
 * Transform API response to UI structure
 * API returns: [{ site_code, site_name, site_latitude, site_longitude, units: [...] }]
 * Transform to: { [areaCode]: { name, sites: [{ code, name, center, zoom, units }] } }
 */
function transformSitesData(apiSites) {
    const grouped = {};

    apiSites.forEach(site => {
        const siteCode = site.code || site.site_code || '';
        const areaCode = siteCode.substring(0, 2).toLowerCase(); // e.g., "SITE_C" → "kp"
        const center = Array.isArray(site.center) && site.center.length === 2
            ? site.center
            : [parseFloat(site.site_latitude), parseFloat(site.site_longitude)];

        const lat = parseFloat(center[0]);
        const lng = parseFloat(center[1]);

        if (!grouped[areaCode]) {
            grouped[areaCode] = {
                name: `Site ${areaCode.toUpperCase()}`,
                sites: []
            };
        }

        // Create site object
        const siteObj = {
            code: siteCode,
            name: site.name || site.site_name || siteCode,
            center: [
                Number.isFinite(lat) ? lat : 0,
                Number.isFinite(lng) ? lng : 0
            ],
            zoom: 15,
            polygon: site.polygon || null,
            basemap_url: site.basemap_url || null,
            coverage_url: site.coverage_url || null,
            units: (site.units || []).map(unit => ({
                id: unit.id,  // FIXED: API returns 'id', not 'device_id'
                name: unit.name,
                fleet_type: unit.fleet_type || 'Heavy Duty Truck',
                // Display status is derived from speed when API doesn't change status
                status: computeDisplayStatus({ status: unit.status, speed: unit.speed }),
                speed: parseFloat(unit.speed) || 0,
                gps: unit.gps,
                satellite: Number.isFinite(Number(unit.satellite)) ? Number(unit.satellite) : null,
                networkQuality: unit.networkQuality || null,
                heading: (unit.heading !== undefined && unit.heading !== null) ? parseFloat(unit.heading) : ((unit.heading_degree !== undefined && unit.heading_degree !== null) ? parseFloat(unit.heading_degree) : null),
                lat: parseFloat(unit.lat ?? unit.current_latitude ?? unit.latitude) || 0,
                lng: parseFloat(unit.lng ?? unit.current_longitude ?? unit.longitude) || 0,
                lastUpdate: unit.lastUpdate || unit.last_update || '-'
            }))
        };

        grouped[areaCode].sites.push(siteObj);
    });

    return grouped;
}

/**
 * Load track history untuk device
 * FIXED: Transform API response to Leaflet-compatible format [[lat,lng], [lat,lng], ...]
 * Also validates data order (must be chronological from database)
 * FIXED: Handle Date object properly and format as Y-m-d string
 */
async function loadTrackHistory(deviceId, startDate = null, endDate = null) {
    // FIXED: Validate deviceId is not undefined
    if (!deviceId) {
        console.warn('❌ loadTrackHistory called with undefined deviceId');
        return null;
    }

    try {
        let startStr = startDate;
        let endStr = endDate;

        // If no dates provided, use selectedDateRange or fallback to today
        if (!startStr || !endStr) {
            if (selectedDateRange && selectedDateRange.start && selectedDateRange.end) {
                startStr = selectedDateRange.start;
                endStr = selectedDateRange.end;
            } else {
                const today = new Date().toISOString().split('T')[0];
                startStr = today;
                endStr = today;
            }
        } else {
            // Handle Date objects
            if (startStr instanceof Date) startStr = startStr.toISOString().split('T')[0];
            if (endStr instanceof Date) endStr = endStr.toISOString().split('T')[0];
        }

        const params = new URLSearchParams({
            start_date: startStr,
            end_date: endStr,
            time_start: '00:00:00',
            time_end: '23:59:59'
        });

        console.log(`🔄 Loading track history for device: ${deviceId}, range: ${startStr} to ${endStr}`);
        const data = await apiCall(`${API_BASE}/GetTrackHistory/${deviceId}?${params}`);

        if (data.status === 'success' && data.track_points && data.track_points.length > 0) {
            // API already returns track_points in Leaflet format: [[lat, lng], [lat, lng], ...]
            // Just validate the coordinates
            const pointsArray = data.track_points.filter(p => {
                // p is already [lat, lng] array, not an object
                if (!Array.isArray(p) || p.length !== 2) {
                    console.debug(`⚠ Invalid point format: ${JSON.stringify(p)}`);
                    return false;
                }

                const lat = parseFloat(p[0]);
                const lng = parseFloat(p[1]);

                // Validate coordinate values
                if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
                    console.debug(`⚠ Invalid coordinates at point: lat=${p[0]}, lng=${p[1]}`);
                    return false;
                }

                // Exclude points with 0.0 coordinates (invalid GPS data)
                // This catches Not Fixed GPS AND any other invalid coordinate scenario
                if (lat === 0.0 || lng === 0.0) {
                    console.debug(`⚠ Excluding invalid GPS coordinate: [${lat}, ${lng}]`);
                    return false;
                }

                return true;
            });

            if (pointsArray.length < 2) {
                console.warn(`⚠ Not enough valid track points for device ${deviceId} (need at least 2, got ${data.track_points.length})`);
                return null;
            }

            // Filter raw_points to exclude invalid coordinates and Not Fixed GPS status
            const validRawPoints = (data.raw_points || []).filter(p => {
                // Exclude Not Fixed GPS status
                if (p.gps_status === 'Not Fixed') {
                    console.debug(`⚠ Excluding raw point with Not Fixed GPS status`);
                    return false;
                }
                // Exclude any records with 0.0 coordinates (invalid GPS)
                if (p.lat === 0.0 || p.lng === 0.0) {
                    console.debug(`⚠ Excluding raw point with invalid GPS coordinate: [${p.lat}, ${p.lng}]`);
                    return false;
                }
                return true;
            }).map(p => {
                p.device_name = data.device?.name || 'Unknown Unit';
                return p;
            });

            // Normalize response with validated points
            const normalizedData = {
                points: pointsArray,  // [[lat,lng], [lat,lng], ...] from API
                raw_points: validRawPoints, // Filtered: Includes speed, heading, and time (excluding invalid GPS)
                summary: data.summary || {},
                device: data.device || {},
                totalPoints: data.meta?.total_points || pointsArray.length
            };

            // Cache the track data only if we have valid points
            TRACK_CACHE[deviceId] = normalizedData;
            console.log(`✓ Track history loaded: ${pointsArray.length} valid points for device ${deviceId} (filtered from ${data.track_points.length})`);
            return normalizedData;
        } else {
            console.warn(`⚠ No track points returned from API for device ${deviceId}`);
        }
    } catch (error) {
        console.error('❌ Error loading track history:', error);
    }

    return null;
}

/**
 * Load current position untuk device
 */
async function loadCurrentPosition(deviceId) {
    try {
        const data = await apiCall(`${API_BASE}/GetCurrentPosition/${deviceId}`);
        return data.status === 'success' ? data.data : null;
    } catch (error) {
        console.error('Error loading current position:', error);
        return null;
    }
}

/**
 * FIXED: Fast position polling untuk selected device (setiap 5 detik)
 * Features:
 * - Load latest position dari database (TOP 1)
 * - Update marker position
 * - Auto-center map on device (follow mode)
 * - Smooth animation, bukan jump
 */
let positionPollingInterval = null;
let mapFollowMode = false;  // Track if map should follow device

/**
 * Smoothly animates a marker from its current position to a new LatLng.
 * @param {L.Marker} marker - The Leaflet marker to animate
 * @param {Array|L.LatLng} destination - The target coordinates [lat, lng]
 * @param {number} durationMs - Animation duration in milliseconds
 */
function slideMarkerTo(marker, destination, durationMs = 1000) {
    if (!marker || !marker.getLatLng || typeof marker.setLatLng !== 'function') return;
    
    if (marker._slideAnimId) {
        cancelAnimationFrame(marker._slideAnimId);
    }
    
    const startLatLng = marker.getLatLng();
    const targetLatLng = L.latLng(destination);
    const startTime = performance.now();
    
    function step(currentTime) {
        const elapsed = currentTime - startTime;
        let progress = elapsed / durationMs;
        
        if (progress > 1) progress = 1;
        
        const currentLat = startLatLng.lat + (targetLatLng.lat - startLatLng.lat) * progress;
        const currentLng = startLatLng.lng + (targetLatLng.lng - startLatLng.lng) * progress;
        
        marker.setLatLng([currentLat, currentLng]);
        
        if (progress < 1) {
            marker._slideAnimId = requestAnimationFrame(step);
        } else {
            marker.setLatLng(targetLatLng);
            marker._slideAnimId = null;
        }
    }
    
    marker._slideAnimId = requestAnimationFrame(step);
}

async function startFastPositionPolling(deviceId) {
    // Clear existing interval
    if (positionPollingInterval) clearInterval(positionPollingInterval);

    // Enable follow mode when polling starts
    mapFollowMode = true;
    console.log(`✓ Map follow mode enabled for device ${deviceId}`);

    positionPollingInterval = setInterval(async () => {
        try {
            if (!selectedUnit || selectedUnit.id !== deviceId) {
                // Device was deselected, stop polling
                stopPositionPolling();
                return;
            }

            // OPTIMIZED: Load LATEST position from database (TOP 1)
            // Note: Site refresh polling will also update this, so we check cache first
            const position = await loadCurrentPosition(deviceId);
            if (!position) {
                console.debug('No position data for device:', deviceId);
                return;
            }

            // Update selectedUnit with fresh data
            selectedUnit.lat = position.latitude;
            selectedUnit.lng = position.longitude;
            selectedUnit.speed = position.speed_kmh;
            selectedUnit.gps = position.gps_status;
            selectedUnit.satellite = Number.isFinite(Number(position.satellite_in_use)) ? Number(position.satellite_in_use) : null;
            selectedUnit.networkQuality = position.network_quality || null;
            selectedUnit.lastUpdate = position.timestamp;
            if (position.fleet_type) {
                selectedUnit.fleet_type = position.fleet_type;
            }

            // FIXED: Validate heading from API, use fallback if null/undefined
            if (position.heading_degree !== null && position.heading_degree !== undefined && !isNaN(position.heading_degree)) {
                selectedUnit.heading = position.heading_degree;
                console.log(`📍 Position updated: [${position.latitude.toFixed(6)}, ${position.longitude.toFixed(6)}], heading: ${position.heading_degree.toFixed(1)}°`);
            } else {
                const prevHeading = selectedUnit.heading !== undefined ? selectedUnit.heading.toFixed(1) : 'Unknown';
                console.warn(`⚠️ Heading from API is invalid (${position.heading_degree}), keeping previous heading: ${prevHeading}°`);
            }
            selectedUnit.status = position.status;

            // Derive display status (ensure speed==0 shows 'idle') and update marker icon live
            const computedStatus = computeDisplayStatus({ status: position.status, speed: position.speed_kmh });
            selectedUnit.status = computedStatus;

            // FIXED: Update LIVE marker position & heading in real-time
            // Live marker shown when just selected, updated by polling
            if (liveMarker && typeof liveMarker.setLatLng === 'function') {
                try {
                    slideMarkerTo(liveMarker, [position.latitude, position.longitude]);
                    updateUnitMarkerHeading(liveMarker, selectedUnit.heading);
                    // Update live marker icon if status changed
                    if (computedStatus && typeof liveMarker !== 'undefined') {
                        updateUnitMarkerIcon(liveMarker, computedStatus, selectedUnit.fleet_type);
                    }
                } catch (err) {
                    console.debug('Live marker update failed:', err.message);
                }
            }

            // Update site view markers if visible (e.g., other units in site)
            const markerObj = unitMarkers.find(m => m.unitId === deviceId);
            if (markerObj && markerObj.marker && typeof markerObj.marker.setLatLng === 'function') {
                try {
                    slideMarkerTo(markerObj.marker, [position.latitude, position.longitude]);
                    updateUnitMarkerHeading(markerObj.marker, selectedUnit.heading);
                    // Update icon immediately if viewing this unit and status changed
                    if (computedStatus && markerObj.previousStatus !== computedStatus) {
                        updateUnitMarkerIcon(markerObj.marker, computedStatus, selectedUnit.fleet_type);
                        markerObj.previousStatus = computedStatus;
                        console.log(`✓ Live status change for viewed unit ${selectedUnit.id}: ${computedStatus}`);
                    }
                    markerObj.marker.setOpacity(1);  // Ensure visible
                } catch (err) {
                    console.debug('Marker update failed:', err.message);
                }
            }

            // FIXED: Also update track end marker position & heading in real-time (when history shown)
            if (trackEndMarker && showingHistoryTrack) {
                try {
                    slideMarkerTo(trackEndMarker, [position.latitude, position.longitude]);
                    // Update heading rotation on track end marker
                    updateUnitMarkerHeading(trackEndMarker, selectedUnit.heading);
                    console.log(`✓ Track end marker updated with heading ${selectedUnit.heading.toFixed(1)}°`);
                } catch (err) {
                    console.debug('Track end marker update failed:', err.message);
                }
            }

            // FIXED: Add real-time dot at new position so dots appear instantly as icon moves
            if (showingHistoryTrack && trackDotLayer && position.network_quality) {
                try {
                    appendLiveDot({
                        lat: position.latitude,
                        lng: position.longitude,
                        network_rssi: position.network_quality.rssi,
                        network_rsrp: position.network_quality.rsrp,
                        network_rsrq: position.network_quality.rsrq,
                        network_sinr: position.network_quality.sinr,
                        speed: position.speed_kmh,
                        time: position.timestamp
                    });
                } catch (err) {
                    console.debug('Live dot append failed:', err.message);
                }
            }

            // FIXED: Always update detail card to keep compass real-time
            // (compass updates even if card is collapsed)
            updateDetailCard(selectedUnit);

            // FIXED: Center map on device if follow mode enabled
            // Only pan if map is in follow mode AND follow mode is still active
            if (mapFollowMode === true && map && selectedUnit) {
                try {
                    const currentCenter = map.getCenter();
                    const newLat = selectedUnit.lat;
                    const newLng = selectedUnit.lng;

                    // Only pan if distance is significant (avoid excessive panning for small movements)
                    const distance = Math.hypot(currentCenter.lat - newLat, currentCenter.lng - newLng);
                    if (distance > 0.0001) { // ~11 meters at equator
                        // Smooth pan to new position (not jump)
                        map.panTo([newLat, newLng], {
                            animate: true,
                            duration: 0.5
                        });
                    }
                    console.log(`✓ Map follow: distance ${(distance * 111).toFixed(0)}m from center`);
                } catch (err) {
                    console.debug('Map pan failed:', err.message);
                }
            }
        } catch (error) {
            console.debug('Position polling error:', error.message);
        }
    }, 10000); // OPTIMIZED: 10 seconds (increased from 5 to reduce API requests)

    console.log(`✓ Fast position polling started: every 10 seconds`);
}

function stopPositionPolling() {
    if (positionPollingInterval) {
        clearInterval(positionPollingInterval);
        positionPollingInterval = null;
    }
    mapFollowMode = false;
    console.log(`✓ Position polling stopped`);
}

function toggleMapFollowMode() {
    mapFollowMode = !mapFollowMode;
    const status = mapFollowMode ? 'ENABLED' : 'DISABLED';
    console.log(`✓ Map follow mode ${status}`);

    // Update UI button if exists
    const btn = document.getElementById('mapFollowToggle');
    if (btn) {
        btn.classList.toggle('active', mapFollowMode);
    }
}

/**
 * FIXED: Refresh total distance for selected unit with fresh track data
 * Load track history from API every 30 seconds (not every 15s) to avoid rate limiting
 * Update distance card and polyline in real-time
 */
async function refreshTotalDistance() {
    if (!selectedUnit) return;

    try {
        // Check if enough time has passed since last track history refresh
        const now = Date.now();
        if (now - lastTrackHistoryRefreshTime < TRACK_HISTORY_REFRESH_INTERVAL) {
            console.debug(`⏭️ Skipping track history refresh (refreshed ${Math.round((now - lastTrackHistoryRefreshTime) / 1000)}s ago, need ${Math.round(TRACK_HISTORY_REFRESH_INTERVAL / 1000)}s)`);
            return;
        }
        lastTrackHistoryRefreshTime = now;

        // Load FRESH track history from API (unit has moved, so track points changed)
        const today = new Date();
        const trackData = await loadTrackHistory(selectedUnit.id, today);

        if (trackData && trackData.points && trackData.points.length > 1) {
            // Calculate distance from fresh track data
            const distance = calculateDistance(trackData.points);
            updateTotalDistance(distance);

            // FIXED: Only update dots if user is viewing history track
            // If not viewing history (showingHistoryTrack = false), don't create dots
            if (showingHistoryTrack) {
                if (trackDotLayer) {
                    map.removeLayer(trackDotLayer);
                    trackDotLayer = null;
                }
                if (trackSegmentLayer) {
                    map.removeLayer(trackSegmentLayer);
                    trackSegmentLayer = null;
                }
                trackDotLayer = buildTrackDotLayer(trackData.raw_points || []);
                trackSegmentLayer = buildTrackSegmentLayer(trackData.raw_points || []);
                if (trackSegmentLayer) trackSegmentLayer.addTo(map);
                if (trackDotLayer) trackDotLayer.addTo(map);
                applyTrackZoomVisualization();

                currentTrackPoints = trackData.points;

                // FIXED: Update track end marker position to current unit position
                // Get the last point from track and update marker
                const lastTrackPoint = trackData.points[trackData.points.length - 1];
                if (trackEndMarker && lastTrackPoint) {
                    try {
                        trackEndMarker.setLatLng(lastTrackPoint);
                        // Update track end marker heading too
                        updateUnitMarkerHeading(trackEndMarker, selectedUnit.heading);
                        console.log(`✓ Total distance updated: ${distance} km, track end marker repositioned`);
                    } catch (err) {
                        console.debug('Track end marker update failed:', err.message);
                    }
                } else {
                    console.log(`✓ Total distance updated: ${distance} km (dots updated)`);
                }
            } else {
                // Not viewing history - just update distance, don't create dots
                console.log(`✓ Total distance updated: ${distance} km (dots not shown - history mode off)`);
            }
        }
    } catch (error) {
        console.debug('⚠ Total distance refresh error:', error.message);
    }
}

/**
 * FIXED: Auto-refresh all sites/units data every 10 seconds
 * Updates:
 * - Unit list for current site
 * - Current positions of ALL unit markers (not just selected unit)
 * - Re-render markers if data changed
 * - Total distance card for selected unit (REAL-TIME)
 */
function startSiteRefreshPolling() {
    // Clear any existing interval
    if (siteRefreshInterval) clearInterval(siteRefreshInterval);

    siteRefreshInterval = setInterval(async () => {
        try {
            // OPTIMIZED: Add rate limiting check - skip if refreshed recently
            const now = Date.now();
            if (now - lastSiteRefreshTime < SITE_REFRESH_INTERVAL) {
                console.debug('⏭️ Skipping site refresh (too soon after last refresh)');
                return;
            }
            lastSiteRefreshTime = now;

            // Reload all sites data from API
            const data = await apiCall(`${API_BASE}/GetSitesWithUnits`);

            if (data.status === 'success' && data.data) {
                const newSitesData = transformSitesData(data.data);

                // If current site is selected, update its units
                if (currentSite && currentArea) {
                    const areaData = newSitesData[currentArea];
                    if (areaData) {
                        const updatedSite = areaData.sites.find(s => s.code === currentSite.code);
                        if (updatedSite) {
                            currentSite = updatedSite;

                            // FIXED: Update ALL unit markers in current site, not just selected
                            updatedSite.units.forEach(updatedUnit => {
                                // Find corresponding marker
                                const markerObj = unitMarkers.find(m => m.unitId === updatedUnit.id);
                                if (markerObj && markerObj.marker && typeof markerObj.marker.setLatLng === 'function') {
                                    try {
                                        // Update marker position with smooth animation
                                        slideMarkerTo(markerObj.marker, [updatedUnit.lat, updatedUnit.lng]);
                                        // FIXED: Compute display status (derive from speed if necessary)
                                        const newDisplayStatus = computeDisplayStatus(updatedUnit);
                                        if (newDisplayStatus && markerObj.previousStatus !== newDisplayStatus) {
                                            updateUnitMarkerIcon(markerObj.marker, newDisplayStatus, updatedUnit.fleet_type);
                                            console.log(`✓ Status changed for ${updatedUnit.name}: ${markerObj.previousStatus || 'unknown'} → ${newDisplayStatus}`);
                                            markerObj.previousStatus = newDisplayStatus;
                                        } else if (!markerObj.previousStatus && newDisplayStatus) {
                                            markerObj.previousStatus = newDisplayStatus;
                                        }

                                        // FIXED: Update marker rotation with consistent heading validation
                                        if (isValidHeading(updatedUnit.heading)) {
                                            updateUnitMarkerHeading(markerObj.marker, updatedUnit.heading);
                                            // FIXED: Cache valid heading per unit for marker recreation - mark as API source
                                            unitHeadingCache[updatedUnit.id] = updatedUnit.heading;
                                            unitHeadingSourceCache[updatedUnit.id] = 'api';
                                        }
                                    } catch (err) {
                                        console.debug(`Marker update skipped for ${updatedUnit.id}:`, err.message);
                                    }
                                }

                                // Also update selected unit if it's this one
                                if (selectedUnit && selectedUnit.id === updatedUnit.id) {
                                    // Update selected unit's position and speed
                                    selectedUnit.lat = updatedUnit.lat;
                                    selectedUnit.lng = updatedUnit.lng;
                                    selectedUnit.speed = updatedUnit.speed;

                                    // FIXED: Only update heading if it's valid (not null/undefined)
                                    if (updatedUnit.heading !== null && updatedUnit.heading !== undefined && !isNaN(updatedUnit.heading)) {
                                        selectedUnit.heading = updatedUnit.heading;
                                    }

                                    // FIXED: Always update detail card to keep compass real-time
                                    // (compass updates even if card is collapsed)
                                    updateDetailCard(selectedUnit);
                                }
                            });

                            // Refresh unit list display
                            const unitList = document.getElementById('unitList');
                            if (unitList) {
                                // Refresh unit items with updated data
                                updateUnitListItems(updatedSite.units);
                            }
                        }
                    }
                }

                // FIXED: Refresh total distance card for selected unit (real-time with fresh API data)
                if (selectedUnit) {
                    await refreshTotalDistance();
                }

                // Update global SITES_DATA
                SITES_DATA = newSitesData;
            }
        } catch (error) {
            console.debug('⚠ Site refresh polling error (retrying):', error.message);
            // Continue polling even if error - just log at debug level
        }
    }, SITE_REFRESH_INTERVAL);

    console.log(`✓ Auto-refresh started: every ${SITE_REFRESH_INTERVAL / 1000} seconds`);
}

function stopSiteRefreshPolling() {
    if (siteRefreshInterval) {
        clearInterval(siteRefreshInterval);
        siteRefreshInterval = null;
        console.log('✓ Auto-refresh stopped');
    }
}

/**
 * Helper to update unit list items with new data
 */
function updateUnitListItems(units) {
    const unitList = document.getElementById('unitList');
    if (!unitList) return;

    // Update each unit item's display (icon, speed, status badge, etc.)
    units.forEach(unit => {
        const unitItem = unitList.querySelector(`[data-unit-id="${unit.id}"]`);
        if (!unitItem) return;

        // Derive display status consistently (speed==0 → idle)
        const displayStatus = computeDisplayStatus(unit);

        // 1. Update icon image (green/yellow/red based on status)
        const iconImg = unitItem.querySelector('.unit-item-icon img');
        if (iconImg) {
            const newIconUrl = getUnitIcon(displayStatus, unit.fleet_type);
            if (iconImg.src !== newIconUrl && !iconImg.src.endsWith(newIconUrl)) {
                iconImg.src = newIconUrl;
            }
        }

        // 2. Update status badge text + icon
        const statusEl = unitItem.querySelector('.unit-item-status');
        if (statusEl) {
            let statusText = '';
            if (displayStatus === 'on') {
                statusText = '<i class="fa-solid fa-truck-fast"></i> On';
            } else if (displayStatus === 'off') {
                statusText = '<i class="fa-solid fa-power-off"></i> Off';
            } else {
                statusText = '<i class="fa-solid fa-square-parking"></i> Idle';
            }
            statusEl.innerHTML = statusText;

            // 3. Update status CSS class (color)
            statusEl.classList.remove('unit-status-on', 'unit-status-off', 'unit-status-idle');
            statusEl.classList.add(`unit-status-${displayStatus}`);
        }

        // 4. Update speed display
        const speedEl = unitItem.querySelector('.unit-speed');
        if (speedEl) {
            speedEl.textContent = `${unit.speed} km/h`;
        }
    });
}

/**
 * FIXED: Update unit marker icon berdasarkan status (warna berbeda)
 * - 'on' (jalan) → hdijo (hijau)
 * - 'idle' (berhenti) → hdkuning (kuning)
 * - 'off' (koneksi mati) → hdmerah (merah)
 */
function updateUnitMarkerIcon(marker, status, fleet_type = "Heavy Duty Truck") {
    if (!marker._icon) return;
    const iconUrl = getUnitIcon(status, fleet_type);
    const img = marker._icon.querySelector('img');
    if (img) {
        img.src = iconUrl;
        console.debug(`🎨 Marker icon updated to ${status} (${iconUrl})`);
    }
}

/**
 * Helper to update unit marker heading direction
 */
function updateUnitMarkerHeading(marker, heading) {
    if (!marker._icon) return;
    const targetRotation = headingToIconRotation(heading);
    const img = marker._icon.querySelector('img');
    if (img) {
        let currentRotation = parseFloat(img.dataset.currentRotation) || 0;
        let diff = (targetRotation - currentRotation) % 360;
        if (diff > 180) diff -= 360;
        if (diff < -180) diff += 360;
        const finalRotation = currentRotation + diff;

        img.dataset.currentRotation = finalRotation;
        img.style.transform = `rotate(${finalRotation}deg)`;
    }
}

function calculateBearing(lat1, lng1, lat2, lng2) {
    const toRad = deg => deg * Math.PI / 180;
    const dLng = toRad(lng2 - lng1);
    const y = Math.sin(dLng) * Math.cos(toRad(lat2));
    const x = Math.cos(toRad(lat1)) * Math.sin(toRad(lat2)) -
        Math.sin(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.cos(dLng);
    let bearing = Math.atan2(y, x) * 180 / Math.PI;
    return (bearing + 360) % 360;
}

function headingToIconRotation(headingDeg) {
    return (headingDeg + 180) % 360;
}

function getPointSignalColor(p, param) {
    const activeParam = (param || window.activeSignalParam || 'rsrp').toLowerCase();
    let val = null;
    if (activeParam === 'rssi') val = p?.network_rssi;
    else if (activeParam === 'rsrq') val = p?.network_rsrq;
    else if (activeParam === 'sinr') val = p?.network_sinr;
    else val = p?.network_rsrp;

    if (typeof classifySignal === 'function') {
        const level = classifySignal(activeParam, val);
        const colors = { excellent: '#3b82f6', good: '#22c55e', fair: '#eab308', poor: '#ef4444', verypoor: '#a855f7' };
        return colors[level] || '#94a3b8';
    }
    return getRsrpColor(val);
}

function getPointSignalLevel(p, param) {
    const activeParam = (param || window.activeSignalParam || 'rsrp').toLowerCase();
    let val = null;
    if (activeParam === 'rssi') val = p?.network_rssi;
    else if (activeParam === 'rsrq') val = p?.network_rsrq;
    else if (activeParam === 'sinr') val = p?.network_sinr;
    else val = p?.network_rsrp;

    if (typeof classifySignal === 'function') {
        return classifySignal(activeParam, val);
    }
    return classifyRsrp(val);
}

function createDotPopupContent(p, activeParam) {
    const normP = normalizePoint(p) || p;
    const timeStr = normP.time ? new Date(normP.time).toLocaleDateString('id-ID', { day: '2-digit', month: 'short', year: 'numeric' }) + ' ' + new Date(normP.time).toLocaleTimeString('id-ID', { hour: '2-digit', minute: '2-digit', second: '2-digit' }) : '-';
    const param = (activeParam || window.activeSignalParam || 'rsrp').toUpperCase();
    const color = getPointSignalColor(normP, activeParam);
    const level = getPointSignalLevel(normP, activeParam);
    const levelLabels = { excellent: 'Excellent', good: 'Good', fair: 'Fair', poor: 'Poor', verypoor: 'Very Poor' };
    const label = levelLabels[level] || 'N/A';

    return `<div style="font-size:12px;line-height:1.7;min-width:160px;">` +
        `<div style="display:flex;align-items:center;gap:6px;margin-bottom:4px;">` +
        `<span style="display:inline-block;width:10px;height:10px;border-radius:50%;background:${color};border:1px solid rgba(0,0,0,0.15);"></span>` +
        `<strong style="color:#1e293b;font-size:13px;">${param} Signal Info</strong>` +
        `<span style="margin-left:auto;font-size:10px;color:${color};font-weight:600;">${label}</span>` +
        `</div>` +
        `<div style="color:#64748b;font-size:11px;margin-bottom:4px;padding-bottom:4px;border-bottom:1px solid #e2e8f0;">` +
        `<strong style="color:#0f172a;"><i class="fa-solid fa-truck-monster me-1"></i> ${normP.device_name || 'Unit Tracking'}</strong><br>` +
        `<i class="fa-regular fa-clock me-1"></i> ${timeStr}` +
        `${normP.speed !== undefined ? ` &nbsp;|&nbsp; <i class="fa-solid fa-gauge me-1"></i> ${normP.speed} km/h` : ''}` +
        `</div>` +
        `<div style="${param === 'RSSI' ? 'font-weight:bold;color:#0f172a;' : ''}">RSSI: <b>${normP.network_rssi ?? '-'}</b> dBm</div>` +
        `<div style="${param === 'RSRP' ? 'font-weight:bold;color:#0f172a;' : ''}">RSRP: <b>${normP.network_rsrp ?? '-'}</b> dBm</div>` +
        `<div style="${param === 'RSRQ' ? 'font-weight:bold;color:#0f172a;' : ''}">RSRQ: <b>${normP.network_rsrq ?? '-'}</b> dB</div>` +
        `<div style="${param === 'SINR' ? 'font-weight:bold;color:#0f172a;' : ''}">SINR: <b>${normP.network_sinr ?? '-'}</b> dB</div></div>`;
}

function getRsrpColor(rsrp) {
    if (rsrp === null || rsrp === undefined || Number.isNaN(Number(rsrp))) {
        return '#94a3b8';
    }

    const val = Number(rsrp);
    if (val >= -85) return '#3b82f6';
    if (val >= -100) return '#22c55e';
    if (val >= -105) return '#eab308';
    if (val >= -115) return '#ef4444';
    return '#a855f7';
}

function classifyRsrp(rsrp) {
    if (rsrp === null || rsrp === undefined || isNaN(Number(rsrp))) return 'unknown';
    const val = Number(rsrp);
    if (val >= -85) return 'excellent';
    if (val >= -100) return 'good';
    if (val >= -105) return 'fair';
    if (val >= -115) return 'poor';
    return 'verypoor';
}

function normalizePoint(p) {
    if (!p) return null;
    if (Array.isArray(p)) {
        return {
            lat: Number(p[0]),
            lng: Number(p[1]),
            network_rsrp: p[2],
            network_rssi: p[3],
            network_rsrq: p[4],
            network_sinr: p[5],
            device_name: p[6] || '',
            time: p[7] || ''
        };
    }
    return {
        lat: Number(p.lat),
        lng: Number(p.lng),
        network_rsrp: p.network_rsrp,
        network_rssi: p.network_rssi,
        network_rsrq: p.network_rsrq,
        network_sinr: p.network_sinr,
        gps_status: p.gps_status,
        device_name: p.device_name || p.unit_name || '',
        time: p.time || p.device_time || ''
    };
}

let globalCanvasRenderer = null;

function buildTrackDotLayer(rawPoints) {
    if (!Array.isArray(rawPoints) || rawPoints.length === 0) return null;
    const activeParam = (window.activeSignalParam || 'rsrp').toLowerCase();

    // 1. Normalize and filter out invalid 0,0 coordinates
    const validPoints = [];
    for (let i = 0; i < rawPoints.length; i++) {
        const p = normalizePoint(rawPoints[i]);
        if (p && Number.isFinite(p.lat) && Number.isFinite(p.lng) && p.lat !== 0 && p.lng !== 0 && p.gps_status !== 'Not Fixed' && p.gps_status !== 'Error') {
            validPoints.push(p);
        }
    }

    if (validPoints.length === 0) return null;

    // 2. Zoom-Aware Spatial Grid Binning for 60 FPS performance when site has 50,000+ points
    const currentZoom = map ? map.getZoom() : 13;
    let displayPoints = validPoints;

    if (validPoints.length > 8000 && currentZoom <= 14) {
        const gridSize = currentZoom <= 12 ? 0.0003 : 0.00015;
        const gridMap = new Map();

        for (let i = 0; i < validPoints.length; i++) {
            const p = validPoints[i];
            const cellKey = `${Math.floor(p.lat / gridSize)},${Math.floor(p.lng / gridSize)}`;
            if (!gridMap.has(cellKey)) {
                gridMap.set(cellKey, p);
            }
        }
        displayPoints = Array.from(gridMap.values());
    }

    // 3. High performance shared Canvas Renderer for GPU acceleration
    if (!globalCanvasRenderer) {
        globalCanvasRenderer = L.canvas({ padding: 0.5 });
    }

    const dots = displayPoints.map(p => {
        const level = getPointSignalLevel(p, activeParam);
        const color = getPointSignalColor(p, activeParam);
        const marker = L.circleMarker([p.lat, p.lng], {
            renderer: globalCanvasRenderer,
            radius: 6,
            weight: 1.5,
            color: '#ffffff',
            fillColor: color,
            fillOpacity: 0.9,
            className: 'rsrp-dot',
            rsrpLevel: level,
            pane: 'trackDotPane'
        });
        marker._rawPoint = p;

        // Lazy popup creation on click for maximum loading speed (15x faster instantiation)
        marker.on('click', function() {
            const param = (window.activeSignalParam || 'rsrp').toLowerCase();
            this.bindPopup(createDotPopupContent(this._rawPoint, param), { offset: [0, -4], closeButton: true, autoClose: true }).openPopup();
        });

        return marker;
    });

    if (dots.length === 0) return null;
    return L.layerGroup(dots);
}

/**
 * Append a single live dot to the existing trackDotLayer in real-time.
 * Called from position polling to show new dots as the unit moves.
 */
function appendLiveDot(p) {
    if (!trackDotLayer || !p || !Number.isFinite(Number(p.lat)) || !Number.isFinite(Number(p.lng))) return;
    const activeParam = (window.activeSignalParam || 'rsrp').toLowerCase();
    const level = getPointSignalLevel(p, activeParam);
    const color = getPointSignalColor(p, activeParam);

    const marker = L.circleMarker([p.lat, p.lng], {
        radius: 7,
        weight: 2,
        color: '#ffffff',
        fillColor: color,
        fillOpacity: 0.9,
        className: 'rsrp-dot',
        rsrpLevel: level,
        pane: 'trackDotPane'
    });
    marker._rawPoint = p;

    marker.bindPopup(createDotPopupContent(p, activeParam), { offset: [0, -4], closeButton: true, autoClose: true });

    trackDotLayer.addLayer(marker);
    console.log(`\u2713 Live dot added at [${p.lat.toFixed(6)}, ${p.lng.toFixed(6)}], ${activeParam.toUpperCase()}: ${color}`);
}

function buildTrackSegmentLayer(rawPoints) {
    // FIXED: Disable track lines as requested, only show dots for signal strength
    return null;
    for (let i = 1; i < rawPoints.length; i++) {
        const prev = rawPoints[i - 1];
        const curr = rawPoints[i];
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
            opacity: 0.9,
            lineCap: 'round',
            lineJoin: 'round',
            interactive: false,
            className: 'rsrp-segment',
            rsrpLevel: classifyRsrp(curr.network_rsrp),
            pane: 'trackLinePane'
        });
        segments.push(segment);
    }

    if (segments.length === 0) return null;
    return L.layerGroup(segments);
}

let map = null;
let currentArea = null;
let currentSite = null;
let selectedUnit = null;
let lastValidHeading = null; // FIXED: Cache last valid heading to prevent 0° flash
let unitMarkers = [];
let siteMarkers = [];
let liveMarker = null; // FIXED: Live position marker (separate from history track)
let trackPolyline = null;
let trackEndMarker = null;
let trackStopMarkers = [];
let trackDotLayer = null;
let trackSegmentLayer = null;
let siteBoundaryLayer = null;
let hiddenMarkerUnitId = null;
let isDetailExpanded = false;
let showingHistoryTrack = false; // FIXED: Track if history track is visible
let unitHeadingCache = {}; // FIXED: Cache valid heading per unit to prevent 0° flash on marker recreation
let unitHeadingSourceCache = {}; // FIXED: Track if heading is from API or just default - prevent caching default 0°
let isProcessingMarkers = false;

// Layer for Basemap KMZ
let currentBasemapKmzLayer = null; // FIXED: Guard against concurrent marker operations (prevent duplicates)
let currentCoveragePlanLayer = null; // Coverage plan KMZ overlay layer
let trackDotsVisible = false;
let unitMarkersVisible = true;
let coveragePlanVisible = false; // Track coverage plan visibility state
let opacityPopupTimeout = null; // Timeout for hiding opacity popup

// Track Hover
let chartHoverMarker = null;

// Auto-refresh polling configuration
let siteRefreshInterval = null;
let lastSiteRefreshTime = 0;
let lastTrackHistoryRefreshTime = 0;
let SITE_REFRESH_INTERVAL = 20000; // 20 seconds for position/unit data update
let TRACK_HISTORY_REFRESH_INTERVAL = 20000; // 20 seconds for track history (same as site refresh for consistent updates)
const RSRP_LINE_ZOOM_THRESHOLD = 16; // Zoom level threshold to switch between dots and lines

// INITIALIZATION
document.addEventListener('DOMContentLoaded', async function () {
    initMap();
    initUnitDetailCard();
    initTrackReplay();
    initUnitSearch();
    initTrackDotToggle();
    initUnitIconsToggle();
    initCoveragePlanToggle();
    initDateRangeSelector();

    // Pre-generate dummy trace data
    initDummyTraceCaches();

    // Load sites data dari API
    await loadSitesFromAPI();

    initSiteSelector();

    showIndonesiaView();

    // START 10-second auto-refresh for all sites/units data
    startSiteRefreshPolling();
});

function initTrackDotToggle() {
    const btn = document.getElementById('trackDotToggle');
    if (!btn) return;

    btn.addEventListener('click', () => {
        trackDotsVisible = !trackDotsVisible;
        setTrackDotsVisible(trackDotsVisible);
        updateTrackDotToggleButton();
    });

    updateTrackDotToggleButton();
}

function initUnitIconsToggle() {
    const btn = document.getElementById('unitToggleBtn');
    if (!btn) return;

    btn.addEventListener('click', () => {
        unitMarkersVisible = !unitMarkersVisible;
        setUnitMarkersVisible(unitMarkersVisible);
        updateUnitIconsToggleButton();
    });

    updateUnitIconsToggleButton();
}

function initCoveragePlanToggle() {
    const btn = document.getElementById('coveragePlanToggle');
    if (!btn) return;

    btn.addEventListener('click', () => {
        toggleCoveragePlan();
    });

    // Initialize opacity slider
    const slider = document.getElementById('coverageOpacitySlider');
    const popup = document.getElementById('coverageOpacityPopup');

    if (slider) {
        slider.addEventListener('input', (e) => {
            const opacity = parseInt(e.target.value) / 100;
            setCoveragePlanOpacity(opacity);

            // Update displayed value
            const valueEl = document.getElementById('coverageOpacityValue');
            if (valueEl) valueEl.textContent = `${e.target.value}%`;

            // Update slider track gradient for visual feedback
            updateSliderTrackGradient(slider, e.target.value);
        });

        // Set initial gradient
        updateSliderTrackGradient(slider, slider.value);
    }

    // Hover logic for opacity popup
    btn.addEventListener('mouseenter', () => {
        if (coveragePlanVisible) {
            clearTimeout(opacityPopupTimeout);
            updateCoverageOpacityPopup(true);
        }
    });

    btn.addEventListener('mouseleave', () => {
        if (coveragePlanVisible) {
            opacityPopupTimeout = setTimeout(() => {
                updateCoverageOpacityPopup(false);
            }, 300);
        }
    });

    if (popup) {
        popup.addEventListener('mouseenter', () => {
            if (coveragePlanVisible) {
                clearTimeout(opacityPopupTimeout);
                updateCoverageOpacityPopup(true);
            }
        });

        popup.addEventListener('mouseleave', () => {
            if (coveragePlanVisible) {
                opacityPopupTimeout = setTimeout(() => {
                    updateCoverageOpacityPopup(false);
                }, 300);
            }
        });
    }

    updateCoveragePlanToggleButton();
}

function updateSliderTrackGradient(slider, value) {
    const pct = value;
    slider.style.background = `linear-gradient(to right, #22c55e 0%, #22c55e ${pct}%, #e2e8f0 ${pct}%, #e2e8f0 100%)`;
}

function toggleCoveragePlan() {
    if (!map || !currentCoveragePlanLayer) {
        console.warn('⚠ No coverage plan layer available to toggle');
        return;
    }

    coveragePlanVisible = !coveragePlanVisible;

    if (coveragePlanVisible) {
        // Apply current opacity before showing
        const slider = document.getElementById('coverageOpacitySlider');
        const opacity = slider ? parseInt(slider.value) / 100 : 0.7;
        currentCoveragePlanLayer.addTo(map);
        setCoveragePlanOpacity(opacity);
        console.log(`✓ Coverage plan shown on map (opacity: ${(opacity * 100).toFixed(0)}%)`);
    } else {
        map.removeLayer(currentCoveragePlanLayer);
        console.log('✓ Coverage plan hidden from map');
    }

    updateCoveragePlanToggleButton();
    updateCoverageOpacityPopup(coveragePlanVisible ? true : false);
}

/**
 * Set opacity for all sub-layers in the coverage KMZ layer group
 * KMZ layers can contain image overlays, tile layers, and vector layers
 */
function setCoveragePlanOpacity(opacity) {
    if (!currentCoveragePlanLayer) return;

    // Try setting opacity on the layer itself
    if (typeof currentCoveragePlanLayer.setOpacity === 'function') {
        currentCoveragePlanLayer.setOpacity(opacity);
    }

    // For layer groups (KMZ typically creates a layerGroup), iterate sub-layers
    if (typeof currentCoveragePlanLayer.eachLayer === 'function') {
        currentCoveragePlanLayer.eachLayer(function (layer) {
            if (typeof layer.setOpacity === 'function') {
                layer.setOpacity(opacity);
            }
            if (typeof layer.setStyle === 'function') {
                layer.setStyle({ opacity: opacity, fillOpacity: opacity * 0.8 });
            }
            // Handle nested layer groups
            if (typeof layer.eachLayer === 'function') {
                layer.eachLayer(function (subLayer) {
                    if (typeof subLayer.setOpacity === 'function') {
                        subLayer.setOpacity(opacity);
                    }
                    if (typeof subLayer.setStyle === 'function') {
                        subLayer.setStyle({ opacity: opacity, fillOpacity: opacity * 0.8 });
                    }
                });
            }
        });
    }

    console.log(`✓ Coverage plan opacity set to ${(opacity * 100).toFixed(0)}%`);
}

function updateCoverageOpacityPopup(forceShow = null) {
    const popup = document.getElementById('coverageOpacityPopup');
    if (!popup) return;

    if (!coveragePlanVisible) {
        popup.classList.remove('visible');
        return;
    }

    if (forceShow === true) {
        popup.classList.add('visible');
    } else if (forceShow === false) {
        popup.classList.remove('visible');
    }
}

function updateCoveragePlanToggleButton() {
    const btn = document.getElementById('coveragePlanToggle');
    if (!btn) return;

    btn.classList.toggle('active', coveragePlanVisible);
    btn.setAttribute('aria-pressed', coveragePlanVisible ? 'true' : 'false');
    btn.title = coveragePlanVisible ? 'Hide Coverage Plan' : 'Show Coverage Plan';
}

function updateUnitIconsToggleButton() {
    const btn = document.getElementById('unitToggleBtn');
    if (!btn) return;

    btn.classList.toggle('active', !unitMarkersVisible);
    btn.setAttribute('aria-pressed', !unitMarkersVisible ? 'true' : 'false');
    btn.title = unitMarkersVisible ? 'Hide unit icons' : 'Show unit icons';
}

function setTrackDotsVisible(visible) {
    const btn = document.getElementById('trackDotToggle');
    if (btn) {
        btn.classList.toggle('active', visible);
        btn.setAttribute('aria-pressed', visible ? 'true' : 'false');
        btn.title = visible ? 'Hide RSRP dots' : 'Show RSRP dots';
        const icon = btn.querySelector('i');
        if (icon) {
            icon.className = visible ? 'fa-solid fa-eye' : 'fa-solid fa-eye-slash';
        }
        btn.classList.remove('eye-animate');
        void btn.offsetWidth;
        btn.classList.add('eye-animate');
    }

    applyTrackZoomVisualization();
}

function setUnitMarkersVisible(visible) {
    unitMarkersVisible = visible;
    unitMarkers.forEach(m => {
        if (map) {
            if (visible) {
                if (!map.hasLayer(m.marker)) {
                    m.marker.addTo(map);
                }
            } else {
                if (map.hasLayer(m.marker)) {
                    map.removeLayer(m.marker);
                }
            }
        }
    });
}

function updateTrackDotToggleButton() {
    const btn = document.getElementById('trackDotToggle');
    if (!btn) return;

    const icon = btn.querySelector('i');
    btn.classList.toggle('active', trackDotsVisible);
    btn.setAttribute('aria-pressed', trackDotsVisible ? 'true' : 'false');
    btn.title = trackDotsVisible ? 'Hide RSRP dots' : 'Show RSRP dots';
    if (icon) icon.className = trackDotsVisible ? 'fa-solid fa-eye' : 'fa-solid fa-eye-slash';
}

function applyTrackZoomVisualization() {
    if (!map) return;

    const allowVisible = trackDotsVisible === true;

    // FIXED: Only use dots, no more lines. Dot size is adjusted dynamically.
    setTrackDotLayerVisible(allowVisible);
    setTrackStopMarkersVisible(allowVisible);
}

function setTrackStopMarkersVisible(visible) {
    if (!trackStopMarkers || !Array.isArray(trackStopMarkers)) return;
    trackStopMarkers.forEach(marker => {
        if (visible) {
            if (!map.hasLayer(marker)) marker.addTo(map);
        } else {
            if (map.hasLayer(marker)) map.removeLayer(marker);
        }
    });
}

function setTrackDotLayerVisible(visible) {
    if (!trackDotLayer) return;

    const filters = window.activeSignalFilters || { excellent: true, good: true, fair: true, poor: true, verypoor: true, nosignal: true };
    const currentZoom = map ? map.getZoom() : 16;
    const activeParam = (window.activeSignalParam || 'rsrp').toLowerCase();
    
    // Dynamic size for dots based on zoom level
    let targetRadius = 7;
    if (currentZoom >= 18) targetRadius = 9;
    else if (currentZoom === 17) targetRadius = 8;
    else if (currentZoom === 16) targetRadius = 7;
    else if (currentZoom === 15) targetRadius = 6;
    else if (currentZoom === 14) targetRadius = 5;
    else if (currentZoom <= 13) targetRadius = 4;

    trackDotLayer.getLayers().forEach(layer => {
        if (typeof layer.setStyle !== 'function') return;
        const p = layer._rawPoint || {};
        const level = getPointSignalLevel(p, activeParam);
        const color = getPointSignalColor(p, activeParam);
        layer.options.rsrpLevel = level;

        const isFilterActive = filters[level] !== false && filters[level === 'verypoor' ? 'nosignal' : level] !== false;

        layer.setStyle({
            fillColor: color,
            opacity: (visible && isFilterActive) ? 1 : 0,
            fillOpacity: (visible && isFilterActive) ? 0.9 : 0,
            radius: targetRadius
        });

        if (typeof layer.setPopupContent === 'function') {
            layer.setPopupContent(createDotPopupContent(p, activeParam));
        }
    });
}

function setTrackSegmentLayerVisible(visible) {
    if (!trackSegmentLayer) return;

    const filters = window.activeSignalFilters || { excellent: true, good: true, fair: true, poor: true, nosignal: true };

    trackSegmentLayer.getLayers().forEach(layer => {
        if (typeof layer.setStyle !== 'function') return;
        const level = layer.options.rsrpLevel || 'unknown';
        const isFilterActive = filters[level] !== false;

        layer.setStyle({
            opacity: (visible && isFilterActive) ? 0.9 : 0
        });
    });
}

window.applySignalFilter = function () {
    if (!map) return;
    applyTrackZoomVisualization();
};

function initMap() {
    map = L.map('trackingMap', {
        center: [-2.5, 118],
        zoom: 5,
        zoomControl: false,
        attributionControl: false,
        preferCanvas: true
    });

    // Create a pane for KMZ basemaps so they sit above the tile layer but below boundaries/markers
    map.createPane('basemapPane');
    map.getPane('basemapPane').style.zIndex = 350; // Below standard overlay pane (400)

    // Create a pane for coverage plan overlays (above basemap, below markers)
    map.createPane('coveragePane');
    map.getPane('coveragePane').style.zIndex = 360;

    // Create a pane for track dots (above coverage, below unit icons)
    map.createPane('trackDotPane');
    map.getPane('trackDotPane').style.zIndex = 500;

    // Create a pane for track lines (above dots, below unit icons)
    map.createPane('trackLinePane');
    map.getPane('trackLinePane').style.zIndex = 550;

    L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
        maxZoom: 19,
    }).addTo(map);

    L.control.zoom({ position: 'bottomright' }).addTo(map);

    map.on('zoomend', applyTrackZoomVisualization);
}

function showIndonesiaView() {
    map.setView([-2.5, 118], 5);
    clearUnitMarkers();
    clearTrack();

    // FIXED: Clear live marker when going back to Indonesia view
    if (liveMarker) {
        map.removeLayer(liveMarker);
        liveMarker = null;
    }

    if (currentBasemapKmzLayer) {
        map.removeLayer(currentBasemapKmzLayer);
        currentBasemapKmzLayer = null;
    }

    if (currentCoveragePlanLayer) {
        map.removeLayer(currentCoveragePlanLayer);
        currentCoveragePlanLayer = null;
    }
    coveragePlanVisible = false;
    updateCoverageOpacityPopup();

    clearSiteMarkers();

    Object.values(SITES_DATA).forEach(area => {
        area.sites.forEach(site => {
            if (!Array.isArray(site.center) || site.center.length !== 2) {
                console.warn('Skipping site with invalid center:', site);
                return;
            }

            const lat = Number(site.center[0]);
            const lng = Number(site.center[1]);
            if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
                console.warn('Skipping site with NaN center:', site);
                return;
            }

            const icon = L.divIcon({
                className: 'site-marker-icon',
                html: `
                    <div class="site-marker-dot"></div>
                    <div class="site-marker-label">${site.code}</div>
                `,
                iconSize: [18, 18],
                iconAnchor: [9, 9],
            });

            const marker = L.marker([lat, lng], { icon }).addTo(map);
            marker.on('click', () => {
                selectSite(site.code);
            });
            siteMarkers.push(marker);
        });
    });
}

function clearSiteMarkers() {
    siteMarkers.forEach(m => map.removeLayer(m));
    siteMarkers = [];
}

// Site Selector
function initSiteSelector() {
    const trigger = document.getElementById('siteDropdownTrigger');
    const list = document.getElementById('siteDropdownList');

    if (!trigger || !list) return;

    const allSites = Object.values(SITES_DATA).flatMap(area => area.sites);
    const triggerText = document.getElementById('siteDropdownText');
    if (triggerText) triggerText.textContent = 'Pilih Site';

    // Remove no-dropdown classes if any
    trigger.classList.remove('no-dropdown');
    list.classList.remove('always-open');

    // Remove old listeners to avoid duplicates
    const newTrigger = trigger.cloneNode(true);
    trigger.parentNode.replaceChild(newTrigger, trigger);

    newTrigger.addEventListener('click', (e) => {
        e.stopPropagation();
        list.classList.toggle('show');
        newTrigger.classList.toggle('open');
    });

    // Close when clicking outside
    document.addEventListener('click', (e) => {
        if (!newTrigger.contains(e.target) && !list.contains(e.target)) {
            list.classList.remove('show');
            newTrigger.classList.remove('open');
        }
    });

    list.innerHTML = '';
    allSites.forEach(site => {
        const opt = document.createElement('div');
        opt.className = 'site-dropdown-option site-dropdown-option-site';
        opt.textContent = `${site.code}`;
        opt.title = site.name;
        opt.dataset.siteCode = site.code;
        opt.addEventListener('click', (e) => {
            e.stopPropagation();
            selectSite(site.code);
            list.classList.remove('show');
            newTrigger.classList.remove('open');
        });
        list.appendChild(opt);
    });
}

function selectArea(areaKey) {
    // FIXED: Stop polling when changing area
    stopPositionPolling();

    currentArea = areaKey;
    const area = SITES_DATA[areaKey];

    const triggerText = document.getElementById('siteDropdownText');
    if (triggerText) triggerText.textContent = area.name;

    document.querySelectorAll('.site-dropdown-option').forEach(opt => {
        opt.classList.toggle('selected', opt.dataset.area === areaKey);
    });

    const grid = document.getElementById('siteButtonsGrid');
    if (grid) {
        grid.innerHTML = '';
        area.sites.forEach(site => {
            const btn = document.createElement('button');
            btn.className = 'site-btn';
            btn.textContent = site.code;
            btn.title = site.name;
            btn.dataset.siteCode = site.code;
            btn.addEventListener('click', () => selectSite(site.code));
            grid.appendChild(btn);
        });
    }

    currentSite = null;
    selectedUnit = null;
    hideChooseUnit();
    hideUnitDetail();
    hideTrackReplay();
    hideTotalDistance();
    hideDateRange();
    clearUnitMarkers();

    // FIXED: Clear live marker when changing area
    if (liveMarker) {
        map.removeLayer(liveMarker);
        liveMarker = null;
    }

    clearTrack();
    clearSearchInput();
}

function selectSite(siteCode) {
    // FIXED: Guard against concurrent marker operations
    if (isProcessingMarkers) {
        console.debug(`⏳ Marker processing already in progress, skip concurrent selectSite`);
        return;
    }

    const siteEntry = Object.entries(SITES_DATA).find(([, area]) => area.sites.find(s => s.code === siteCode));
    if (!siteEntry) return;

    // FIXED: Stop polling when changing site
    stopPositionPolling();

    const [areaKey, area] = siteEntry;
    currentArea = areaKey;

    const site = area.sites.find(s => s.code === siteCode);
    if (!site) return;

    const triggerText = document.getElementById('siteDropdownText');
    if (triggerText) triggerText.textContent = `${site.code} — ${site.name}`;

    currentSite = site;
    selectedUnit = null;

    document.querySelectorAll('.site-btn').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.siteCode === siteCode);
    });

    isProcessingMarkers = true; // FIXED: Lock to prevent concurrent operations
    try {
        clearSiteMarkers();
        clearUnitMarkers();

        // FIXED: Clear live marker when changing site
        if (liveMarker) {
            map.removeLayer(liveMarker);
            liveMarker = null;
        }

        clearTrack();
        hideUnitDetail();
        hideTrackReplay();
        hideTotalDistance();
        clearSearchInput();

        // Clear existing site boundary
        if (siteBoundaryLayer) {
            map.removeLayer(siteBoundaryLayer);
            siteBoundaryLayer = null;
        }

        // Draw new site boundary if exists
        if (site.polygon && site.polygon.length > 0) {
            try {
                // Leaflet requires [lat, lng] format, which is how we stored it from backend
                siteBoundaryLayer = L.polygon(site.polygon, {
                    color: '#ff0000ff',
                    weight: 3.5,
                    opacity: 1,
                    fillColor: '#D4952B',
                    fillOpacity: 0,
                    dashArray: '2, 10'
                }).addTo(map);
            } catch (e) {
                console.error("Failed to draw site polygon:", e);
            }
        }

        // Clear existing basemap KMZ
        if (currentBasemapKmzLayer) {
            map.removeLayer(currentBasemapKmzLayer);
            currentBasemapKmzLayer = null;
        }

        console.log('Checking basemap for site:', site.code, 'URL:', site.basemap_url);
        // Load new basemap KMZ if exists
        if (site.basemap_url && typeof L.kmzLayer === 'function') {
            try {
                console.log('Attempting to load Basemap KMZ from:', site.basemap_url);
                currentBasemapKmzLayer = L.kmzLayer(site.basemap_url, {
                    pane: 'basemapPane'
                });
                
                currentBasemapKmzLayer.on('load', function(e) {
                    console.log('✅ Basemap KMZ loaded successfully! Features loaded:', e);
                });

                currentBasemapKmzLayer.addTo(map);
            } catch (e) {
                console.error("❌ Exception when initializing basemap KMZ:", e);
            }
        } else if (!site.basemap_url) {
            console.log('ℹ️ No basemap_url for this site.');
        } else if (typeof L.kmzLayer !== 'function') {
            console.error('❌ L.kmzLayer function is missing! leaflet-kmz plugin not loaded properly.');
        }

        // Clear existing coverage plan layer
        if (currentCoveragePlanLayer) {
            map.removeLayer(currentCoveragePlanLayer);
            currentCoveragePlanLayer = null;
        }
        coveragePlanVisible = false;
        updateCoverageOpacityPopup();

        // Load coverage plan KMZ if exists (but don't show by default)
        console.log('Checking coverage for site:', site.code, 'URL:', site.coverage_url);
        if (site.coverage_url && typeof L.kmzLayer === 'function') {
            try {
                console.log('Attempting to load Coverage Plan KMZ from:', site.coverage_url);
                currentCoveragePlanLayer = L.kmzLayer(site.coverage_url, {
                    pane: 'coveragePane'
                });
                
                currentCoveragePlanLayer.on('load', function(e) {
                    console.log('✅ Coverage Plan KMZ loaded successfully! Features loaded:', e);
                    // Don't add to map yet - user must toggle via button
                });

                // Pre-load but don't display
                currentCoveragePlanLayer.addTo(map);
                map.removeLayer(currentCoveragePlanLayer);
            } catch (e) {
                console.error('❌ Exception when initializing coverage plan KMZ:', e);
            }
        } else if (!site.coverage_url) {
            console.log('ℹ️ No coverage_url for this site.');
        }

        // Show date range card when site is selected
        showDateRange();

        // Hide back button, show unit toggle and coverage toggle
        const backBtn = document.getElementById('mapBackBtn');
        if (backBtn) backBtn.classList.add('hidden-btn');
        const unitBtn = document.getElementById('unitToggleBtn');
        if (unitBtn) unitBtn.classList.remove('hidden-btn');
        const coverageBtn = document.getElementById('coveragePlanToggle');
        if (coverageBtn) {
            if (site.coverage_url) {
                coverageBtn.classList.remove('hidden-btn');
            } else {
                coverageBtn.classList.add('hidden-btn');
            }
        }
        updateCoveragePlanToggleButton();

        // Add unit markers with heading-based rotation
        site.units.forEach(unit => {
            addUnitMarker(unit);
        });
        setUnitMarkersVisible(unitMarkersVisible);

        // Show site level tracks immediately using current date range
        showSiteLevelTracks();

        // FIXED: Update toggle button for site mode
        updateTrackDotToggleButton();

        // Zoom to fit site boundary OR devices
        if (siteBoundaryLayer) {
            const bounds = siteBoundaryLayer.getBounds();
            // Optional: Include device markers in bounds if any
            if (unitMarkers.length > 0) {
                unitMarkers.forEach(m => {
                    const latLng = m.marker.getLatLng();
                    if (latLng.lat !== 0 && latLng.lng !== 0) {
                        bounds.extend(latLng);
                    }
                });
            }
            map.fitBounds(bounds, {
                padding: [50, 50],
                animate: true,
                duration: 1
            });
            console.log(`✓ Site view: zoomed to fit site boundary`);
        } else if (unitMarkers.length > 0) {
            // Calculate bounds from all valid device markers
            const validMarkers = unitMarkers.filter(m => {
                const latLng = m.marker.getLatLng();
                return latLng.lat !== 0 && latLng.lng !== 0;
            });

            if (validMarkers.length > 0) {
                const markerBounds = L.latLngBounds(
                    validMarkers.map(m => m.marker.getLatLng())
                );
                // Include site center in bounds to prevent extreme zoom
                if (site.center && (site.center[0] !== 0 || site.center[1] !== 0)) {
                    markerBounds.extend(site.center);
                }
                
                // Zoom to fit all markers with padding
                map.fitBounds(markerBounds, {
                    padding: [80, 80],
                    maxZoom: 16,
                    animate: true,
                    duration: 1
                });
                console.log(`✓ Site view: zoomed to fit ${validMarkers.length} devices`);
            } else {
                map.flyTo(site.center, site.zoom, { duration: 1.2 });
            }
        } else {
            // No markers and no boundary, just go to site center
            map.flyTo(site.center, site.zoom, { duration: 1.2 });
        }

        showChooseUnit(site.units);
    } finally {
        isProcessingMarkers = false; // FIXED: Unlock after done
    }
}

/**
 * FIXED: Check if heading value is truly valid (0-360 are all valid bearings)
 * null/undefined/NaN = invalid heading data (not available yet)
 */
function isValidHeading(heading) {
    return heading !== null &&
        heading !== undefined &&
        !isNaN(heading) &&
        heading >= 0 &&
        heading <= 360;
}

function addUnitMarker(unit) {
    if (!unit || (unit.lat === 0 && unit.lng === 0) || unit.has_gps_error || unit.gps === 'Not Fixed' || unit.gps === 'Error') {
        console.warn(`⚠️ Unit ${unit?.name} (${unit?.id}) mengalami GPS Error / lat-lng 0,0, skip marker pada peta`);
        return;
    }

    // FIX #1: CHECK FIRST - Apakah marker untuk unit ini sudah ada?
    const existingMarker = unitMarkers.find(m => m.unitId === unit.id);
    if (existingMarker) {
        console.debug(`⚠️ Marker untuk ${unit.name} (${unit.id}) sudah ada, skip duplicate`);
        return; // JANGAN add 2x
    }

    // Derive display status (so speed==0 maps to 'idle')
    const displayStatus = computeDisplayStatus(unit);
    const iconUrl = getUnitIcon(displayStatus, unit.fleet_type);

    // FIX #2: Proper heading validation - use isValidHeading() consistently
    let markerHeading = unit.heading;
    let headingSource = 'unit'; // Track source of heading

    // ONLY use cache if heading is truly missing (null/undefined/NaN)
    if (!isValidHeading(markerHeading)) {
        // FIXED: Try unit-specific cache ONLY if it came from API before (not default 0°)
        if (unitHeadingSourceCache[unit.id] === 'api' && isValidHeading(unitHeadingCache[unit.id])) {
            markerHeading = unitHeadingCache[unit.id];
            headingSource = 'cache-api';
            console.debug(`🧭 Using cached API heading for marker ${unit.id}: ${markerHeading.toFixed(0)}°`);
        }
        // Fallback to global cache if no unit-specific cache from API
        else if (isValidHeading(lastValidHeading)) {
            markerHeading = lastValidHeading;
            headingSource = 'cache-global';
            console.debug(`🧭 Using global cached heading for marker: ${markerHeading.toFixed(0)}°`);
        } else {
            // TRULY no heading data yet - use 0 temporarily (akan update saat polling)
            markerHeading = 0;
            headingSource = 'default';
            console.debug(`⚠️ No heading data yet for ${unit.name}, using 0° (will update on polling)`);
        }
    } else {
        // FIXED: Save valid heading to unit-specific cache ONLY if from API (not default)
        // Mark that this heading came from API, so we can restore it on recreation
        unitHeadingCache[unit.id] = markerHeading;
        unitHeadingSourceCache[unit.id] = 'api';
        console.debug(`✓ Heading from API cached for ${unit.id}: ${markerHeading.toFixed(0)}°`);
    }

    const rotation = headingToIconRotation(markerHeading);
    const icon = L.divIcon({
        className: 'unit-marker-icon',
        html: `
            <div class="unit-marker-wrapper">
                <img src="${iconUrl}" alt="${unit.name}" data-current-rotation="${rotation}" style="transform: rotate(${rotation}deg);">
            </div>
        `,
        iconSize: [52, 52],
        iconAnchor: [26, 26],
    });

    const marker = L.marker([unit.lat, unit.lng], { icon, zIndexOffset: 1000 }).addTo(map);
    marker.on('click', () => selectUnit(unit.id));
    // Store with unitId for later show/hide & deduplication
    unitMarkers.push({
        marker,
        unitId: unit.id,
        previousStatus: displayStatus  // Track previous display status for change detection
    });
    console.log(`✓ Marker added for ${unit.name} (status: ${displayStatus}, heading source: ${headingSource})`);
}

function clearUnitMarkers() {
    unitMarkers.forEach(m => map.removeLayer(m.marker));
    unitMarkers = [];
    hiddenMarkerUnitId = null;
}

// Hide the unit marker for a specific unitId (to avoid overlap with track end marker)
function hideUnitMarkerById(unitId) {
    unitMarkers.forEach(m => {
        if (m.unitId === unitId) {
            map.removeLayer(m.marker);
        }
    });
    hiddenMarkerUnitId = unitId;
}

// Re-show a previously hidden unit marker
function restoreHiddenMarker() {
    if (!hiddenMarkerUnitId || !currentSite) return;
    const unit = currentSite.units.find(u => u.id === hiddenMarkerUnitId);
    if (unit) {
        // Check if marker still exists in array
        const existing = unitMarkers.find(m => m.unitId === hiddenMarkerUnitId);
        if (existing) {
            existing.marker.addTo(map);
        }
    }
    hiddenMarkerUnitId = null;
}

// Show only the selected unit on map (all others hidden)
function showOnlySelectedUnitMarker(unitId) {
    unitMarkers.forEach(m => {
        if (!m?.marker) return;

        if (m.unitId === unitId) {
            // Selected base marker stays hidden (replaced by liveMarker)
            map.removeLayer(m.marker);
            return;
        }

        map.removeLayer(m.marker);
    });
}

// Choose Unit
function showChooseUnit(units) {
    const card = document.getElementById('chooseUnitCard');
    const list = document.getElementById('unitList');
    if (!card || !list) return;

    list.innerHTML = '';
    if (!Array.isArray(units) || units.length === 0) {
        card.classList.remove('visible');
        return;
    }

    const fragment = document.createDocumentFragment();

    units.forEach(unit => {
        const isGpsError = (unit.lat === 0 && unit.lng === 0) || unit.has_gps_error || unit.gps === 'Not Fixed' || unit.gps === 'Error';
        let statusClass = `unit-status-${unit.status}`;
        let statusText = '';

        if (isGpsError) {
            statusClass = 'unit-status-error';
            statusText = '<i class="fa-solid fa-triangle-exclamation me-1"></i> GPS Error';
        } else if (unit.status === 'on') {
            statusText = '<i class="fa-solid fa-truck-fast me-1"></i> On';
        } else if (unit.status === 'off') {
            statusText = '<i class="fa-solid fa-power-off me-1"></i> Off';
        } else {
            statusText = '<i class="fa-solid fa-square-parking me-1"></i> Idle';
        }

        const iconUrl = getUnitIcon(unit.status, unit.fleet_type);

        const item = document.createElement('div');
        item.className = 'unit-item';
        item.dataset.unitId = unit.id;
        item.dataset.unitName = unit.name.toLowerCase();
        item.innerHTML = `
            <div class="unit-item-icon">
                <img src="${iconUrl}" alt="${unit.name}">
            </div>
            <span class="unit-item-name">${unit.name}</span>
            <span class="unit-item-status ${statusClass}">${statusText}</span>
        `;
        item.addEventListener('click', () => {
            if (isGpsError) {
                Swal.fire({
                    icon: 'error',
                    title: 'GPS Error (0.0, 0.0)',
                    text: `Unit "${unit.name}" mengalami GPS Error (koordinat 0.0, 0.0 / Not Fixed) sehingga posisi tidak dapat ditampilkan pada peta.`,
                    confirmButtonColor: '#d33',
                    confirmButtonText: 'Mengerti'
                });
            } else {
                selectUnit(unit.id);
            }
        });
        fragment.appendChild(item);
    });

    list.appendChild(fragment);
    card.classList.add('visible');
}

function hideChooseUnit() {
    const card = document.getElementById('chooseUnitCard');
    if (card) card.classList.remove('visible');
}

// Unit Search
function initUnitSearch() {
    const searchInput = document.getElementById('unitSearchInput');
    if (!searchInput) return;

    searchInput.addEventListener('input', function () {
        const query = this.value.toLowerCase().trim();
        const items = document.querySelectorAll('#unitList .unit-item');
        items.forEach(item => {
            const name = item.dataset.unitName || '';
            item.style.display = name.includes(query) ? '' : 'none';
        });
    });
}

function clearSearchInput() {
    const searchInput = document.getElementById('unitSearchInput');
    if (searchInput) {
        searchInput.value = '';
        // Show all items
        document.querySelectorAll('#unitList .unit-item').forEach(item => {
            item.style.display = '';
        });
    }
}

// Select Unit
/**
 * FIXED: Select device unit - Load fresh position + center map + start tracking
 */
async function selectUnit(unitId) {
    // FIXED: Validate unitId is not undefined
    if (!unitId) {
        console.warn('❌ selectUnit called with undefined unitId');
        return;
    }

    if (!currentSite) return;
    const unit = currentSite.units.find(u => u.id === unitId);
    if (!unit) {
        console.warn(`❌ Unit not found: ${unitId}`);
        return;
    }

    console.log(`📍 Selecting device: ${unit.name} (${unitId})`);

    // Stop any existing polling from previous unit
    stopPositionPolling();

    // Hide date range card when unit is selected
    hideDateRange();

    // FIXED: Load LATEST position from database immediately
    // Skip for dummy units (they don't exist in API)
    const isDummyUnit = unitId.startsWith('DUMMY-');
    if (!isDummyUnit) {
        try {
            const latestPos = await loadCurrentPosition(unitId);
            if (latestPos) {
                // Update unit object with fresh data
                unit.lat = latestPos.latitude;
                unit.lng = latestPos.longitude;
                unit.speed = latestPos.speed_kmh;
                // Compute display status immediately (speed==0 → 'idle', not raw API 'on')
                unit.status = computeDisplayStatus({ status: latestPos.status, speed: latestPos.speed_kmh });
                unit.lastUpdate = latestPos.timestamp;
                if (latestPos.fleet_type) {
                    unit.fleet_type = latestPos.fleet_type;
                }

                // FIX #2: ONLY update heading jika truly valid
                if (isValidHeading(latestPos.heading_degree)) {
                    unit.heading = latestPos.heading_degree;
                    lastValidHeading = latestPos.heading_degree;  // Cache it
                    console.log(`✓ Latest position loaded: [${latestPos.latitude}, ${latestPos.longitude}], heading: ${latestPos.heading_degree.toFixed(1)}°`);
                } else {
                    // Don't set heading - keep undefined/previous value
                    console.warn(`⚠️ Heading unavailable from API, will use polling data when available`);
                }
            }
        } catch (err) {
            console.debug('⚠ Could not load fresh position, will use polling:', err.message);
        }
    } else {
        // For dummy units, ensure heading is valid
        if (isValidHeading(unit.heading)) {
            lastValidHeading = unit.heading;
        }
    }

    selectedUnit = unit;

    // FIXED: Reset track history refresh timer so distance updates immediately on first refresh
    lastTrackHistoryRefreshTime = 0;
    console.log(`✓ Track refresh timer reset - will update immediately on first polling`);

    // Active state in list
    document.querySelectorAll('.unit-item').forEach(item => {
        item.classList.toggle('active', item.dataset.unitId === unitId);
    });

    // FIXED: Center map on device immediately
    map.setView([unit.lat, unit.lng], 16, { animate: true });
    console.log(`✓ Map centered on device`);

    // FIXED: Hide the base marker from unitMarkers to prevent duplicate overlapping markers
    restoreHiddenMarker();
    hideUnitMarkerById(unitId);
    showOnlySelectedUnitMarker(unitId);

    // Show live marker (current position)
    showLiveMarker(unit);

    // Show back button
    const backBtn = document.getElementById('mapBackBtn');
    if (backBtn) {
        backBtn.style.display = ''; // Clear inline display if any
        backBtn.classList.remove('hidden-btn');
    }

    // Hide unit icons toggle button
    const unitBtn = document.getElementById('unitToggleBtn');
    if (unitBtn) unitBtn.classList.add('hidden-btn');

    // Hide coverage plan toggle button when viewing single unit
    const coverageBtn = document.getElementById('coveragePlanToggle');
    if (coverageBtn) coverageBtn.classList.add('hidden-btn');

    // FIXED: Always enable history track mode when selecting a unit
    // This ensures refreshTotalDistance will create dots even if initial load has no data yet
    showingHistoryTrack = true;

    // FIXED: Show history dots on live map (same as replay)
    showHistoryTrack(unitId, { keepLiveMarker: true, showEndMarker: false, fitBounds: false });

    updateTrackDotToggleButton();

    // Show detail card
    showUnitDetail(unit);

    // Show track replay and total distance
    showTrackReplay();
    showTotalDistance();

    // FIXED: Start FAST position polling with map follow
    // Skip for dummy units
    if (!isDummyUnit) {
        startFastPositionPolling(unitId);
    }
}

function backToSite() {
    if (!currentSite) return;

    // Hide back button immediately for smooth animation
    const backBtn = document.getElementById('mapBackBtn');
    if (backBtn) backBtn.classList.add('hidden-btn');

    // Re-select the site to reset view
    selectSite(currentSite.code);
}

// FIXED: Show LIVE position marker only (no history track)
// This is shown when unit is selected, to see real-time position
function showLiveMarker(unit) {
    if (!unit) return;

    // Remove existing live marker
    if (liveMarker) {
        map.removeLayer(liveMarker);
        liveMarker = null;
    }

    // FIX #2: Use valid heading or fallback to cache, NEVER use invalid 0
    let markerHeading = isValidHeading(unit.heading) ? unit.heading : lastValidHeading;

    // Only use 0 if ABSOLUTELY no heading data available
    if (!isValidHeading(markerHeading)) {
        markerHeading = 0; // Will show arbitrary rotation, will update on polling
    }

    const iconUrl = getUnitIcon(computeDisplayStatus(unit), unit.fleet_type);
    const rotation = headingToIconRotation(markerHeading);

    const liveIcon = L.divIcon({
        className: 'unit-marker-icon',
        html: `
            <div class="unit-marker-wrapper">
                <div class="marker-pulse"></div>
                <img src="${iconUrl}" alt="${unit.name}" data-current-rotation="${rotation}" style="transform: rotate(${rotation}deg);">
            </div>
        `,
        iconSize: [52, 52],
        iconAnchor: [26, 26],
    });

    liveMarker = L.marker([unit.lat, unit.lng], { icon: liveIcon, zIndexOffset: 1000 }).addTo(map);
    console.log(`✓ Live marker shown at [${unit.lat.toFixed(6)}, ${unit.lng.toFixed(6)}], heading: ${markerHeading.toFixed(0)}°`);
}

// FIXED: Show HISTORY track with polyline (user requested via track replay)
// This is shown only when user clicks "Set" button
async function showHistoryTrack(unitId, options = {}) {
    const {
        keepLiveMarker = false,
        showEndMarker = true,
        fitBounds = true
    } = options;
    // FIXED: Validate unitId before proceeding
    if (!unitId) {
        console.warn('❌ showHistoryTrack called with undefined unitId');
        return;
    }

    // Clear existing history (if showing old one)
    if (trackPolyline) {
        map.removeLayer(trackPolyline);
        trackPolyline = null;
    }
    if (trackDotLayer) {
        map.removeLayer(trackDotLayer);
        trackDotLayer = null;
    }
    if (trackSegmentLayer) {
        map.removeLayer(trackSegmentLayer);
        trackSegmentLayer = null;
    }
    if (trackSegmentLayer) {
        map.removeLayer(trackSegmentLayer);
        trackSegmentLayer = null;
    }
    if (trackEndMarker) {
        map.removeLayer(trackEndMarker);
        trackEndMarker = null;
    }

    // FIXED: Clear existing stop markers
    if (trackStopMarkers && trackStopMarkers.length > 0) {
        trackStopMarkers.forEach(m => map.removeLayer(m));
        trackStopMarkers = [];
    }

    if (!selectedUnit) return;

    // Try to get cached track data, otherwise load from API
    let trackData = TRACK_CACHE[unitId];
    if (!trackData || !Array.isArray(trackData.raw_points) || trackData.raw_points.length === 0) {
        trackData = await loadTrackHistory(unitId);
    }

    // FIXED: Only show track if we have real API data (no random fallback)
    if (!trackData || !trackData.points || trackData.points.length < 2) {
        console.warn(`⚠ No valid track data for unit ${unitId}. No track will be displayed.`);
        // Show empty state in UI
        return;
    }

    const points = trackData.points;

    if (points && points.length > 1) {
        trackDotLayer = buildTrackDotLayer(trackData.raw_points || []);
        trackSegmentLayer = buildTrackSegmentLayer(trackData.raw_points || []);
        if (trackSegmentLayer) trackSegmentLayer.addTo(map);
        if (trackDotLayer) trackDotLayer.addTo(map);
        applyTrackZoomVisualization();

        // Store track points for chart hover
        currentTrackPoints = points;

        // FIXED: Don't override heading from track bearing!
        // Keep heading from current position (selectedUnit.heading) which is more accurate for real-time
        // Calculate track heading only for reference, don't update unit heading
        const p1 = points[points.length - 2];
        const p2 = points[points.length - 1];
        const trackHeading = calculateBearing(p1[0], p1[1], p2[0], p2[1]);
        console.log(`Track bearing: ${trackHeading.toFixed(1)}°, Unit heading: ${selectedUnit.heading.toFixed(1)}°`);

        // FIXED: Hide the live marker when showing history track
        // Both won't show together - cleaner UI
        if (!keepLiveMarker && liveMarker) {
            map.removeLayer(liveMarker);
        }

        if (showEndMarker) {
            // Place truck icon at the END of the track line, rotated to CURRENT heading (not track bearing)
            const lastPoint = p2;
            const iconUrl = getUnitIcon(selectedUnit.status, selectedUnit.fleet_type);
            const rotation = headingToIconRotation(selectedUnit.heading); // Use current heading, not track bearing
            const endIcon = L.divIcon({
                className: 'unit-marker-icon',
                html: `
                    <div class="unit-marker-wrapper">
                        <img src="${iconUrl}" alt="${selectedUnit.name}" data-current-rotation="${rotation}" style="transform: rotate(${rotation}deg);">
                    </div>
                `,
                iconSize: [52, 52],
                iconAnchor: [26, 26],
            });
            trackEndMarker = L.marker(lastPoint, { icon: endIcon, zIndexOffset: 1000 }).addTo(map);
        }

        // Update detail card image rotation with current heading
        updateDetailImageRotation(selectedUnit.heading);

        // Fit map to show entire track
        if (fitBounds) {
            const bounds = L.latLngBounds(points);
            map.fitBounds(bounds, { padding: [50, 50] });
        }

        // Calculate and display distance
        const distance = calculateDistance(points);
        updateTotalDistance(distance);

        // FIXED: Calculate and show idle/stop points from raw API data
        if (trackData.raw_points && trackData.raw_points.length > 0) {
            let currentStop = null;
            const stops = [];

            trackData.raw_points.forEach(p => {
                if (p.speed === 0) {
                    if (!currentStop) {
                        // Start of a new stop
                        currentStop = {
                            lat: p.lat,
                            lng: p.lng,
                            startTime: new Date(p.time),
                            endTime: new Date(p.time),
                            count: 1
                        };
                    } else {
                        // Continue stop
                        currentStop.endTime = new Date(p.time);
                        currentStop.count++;
                    }
                } else {
                    if (currentStop) {
                        // Stop ended, check if duration > 2 mins
                        const durationMs = currentStop.endTime - currentStop.startTime;
                        if (durationMs >= 120000) {
                            stops.push(currentStop);
                        }
                        currentStop = null;
                    }
                }
            });
            // Handle if track ends while still stopped
            if (currentStop) {
                const durationMs = currentStop.endTime - currentStop.startTime;
                if (durationMs >= 120000) {
                    stops.push(currentStop);
                }
            }

            // Draw markers for valid stops
            stops.forEach((stop, index) => {
                const durationMinutes = Math.floor((stop.endTime - stop.startTime) / 60000);
                const durationHours = Math.floor(durationMinutes / 60);
                const durationMins = durationMinutes % 60;

                let durationText = '';
                if (durationHours > 0) {
                    durationText += `${durationHours} jam `;
                }
                durationText += `${durationMins} mnt`;

                const stopIcon = L.divIcon({
                    className: 'stop-marker-icon',
                    html: `<div class="stop-marker-dot"><i class="fa-solid fa-square-parking"></i></div>`,
                    iconSize: [26, 26],
                    iconAnchor: [13, 13]
                });

                const marker = L.marker([stop.lat, stop.lng], { icon: stopIcon, zIndexOffset: 500 });
                if (trackDotsVisible) {
                    marker.addTo(map);
                }
                marker.bindTooltip(`
                    <div style="text-align: center; line-height: 1.4;">
                        <strong><i class="fa-solid fa-square-parking" style="color:#ef4444;margin-right:4px;"></i> Idle / Stop #${index + 1}</strong><br>
                        Durasi: ${durationText}<br>
                        <span style="font-size:10px;color:#64748b;">${stop.startTime.toLocaleTimeString('id-ID', { hour: '2-digit', minute: '2-digit' })} - ${stop.endTime.toLocaleTimeString('id-ID', { hour: '2-digit', minute: '2-digit' })}</span>
                    </div>
                `, { direction: 'top', offset: [0, -10] });

                trackStopMarkers.push(marker);
            });
            console.log(`✓ Generated ${stops.length} idle/stop markers`);
        }

        showingHistoryTrack = true;
        console.log(`✓ History track shown with ${points.length} points`);
    }
}

// DEPRECATED: Old function - now split into showLiveMarker + showHistoryTrack
async function showTrack(unitId) {
    console.warn(`⚠️ showTrack() is deprecated. Use showLiveMarker() or showHistoryTrack() instead.`);
    showHistoryTrack(unitId);
}

function clearTrack() {
    // Remove history track line
    if (trackPolyline) {
        map.removeLayer(trackPolyline);
        trackPolyline = null;
    }
    if (trackDotLayer) {
        map.removeLayer(trackDotLayer);
        trackDotLayer = null;
    }
    if (trackSegmentLayer) {
        map.removeLayer(trackSegmentLayer);
        trackSegmentLayer = null;
    }
    // Remove track-end marker
    if (trackEndMarker) {
        map.removeLayer(trackEndMarker);
        trackEndMarker = null;
    }
    // Remove stop markers
    if (trackStopMarkers && trackStopMarkers.length > 0) {
        trackStopMarkers.forEach(m => map.removeLayer(m));
        trackStopMarkers = [];
    }
    currentTrackPoints = null;

    // FIXED: Restore live marker when clearing history track
    if (selectedUnit) {
        showLiveMarker(selectedUnit);
    }

    showingHistoryTrack = false;
    console.log(`✓ History track cleared`);
}

function generateRandomTrack(endLat, endLng) {
    const points = [];
    let lat = endLat + 0.008;
    let lng = endLng - 0.006;

    const steps = 15;
    const latStep = (endLat - lat) / steps;
    const lngStep = (endLng - lng) / steps;

    for (let i = 0; i <= steps; i++) {
        const jitterLat = (Math.random() - 0.5) * 0.0004;
        const jitterLng = (Math.random() - 0.5) * 0.0004;
        points.push([
            lat + latStep * i + (i < steps ? jitterLat : 0),
            lng + lngStep * i + (i < steps ? jitterLng : 0)
        ]);
    }
    // Ensure the last point is exactly the unit position
    points[points.length - 1] = [endLat, endLng];
    return points;
}

function calculateDistance(points) {
    let total = 0;
    for (let i = 1; i < points.length; i++) {
        const latlng1 = L.latLng(points[i - 1]);
        const latlng2 = L.latLng(points[i]);
        total += latlng1.distanceTo(latlng2);
    }
    return (total / 1000).toFixed(1);
}

// Unit Detail Card
function initUnitDetailCard() {
    const collapsed = document.getElementById('unitDetailCollapsed');
    const expanded = document.getElementById('unitDetailExpanded');
    const closeBtn = document.getElementById('unitDetailClose');

    if (collapsed) {
        collapsed.addEventListener('click', () => {
            collapsed.style.display = 'none';
            expanded.classList.add('show');
            isDetailExpanded = true;
        });
    }

    if (closeBtn) {
        closeBtn.addEventListener('click', () => {
            expanded.classList.remove('show');
            collapsed.style.display = 'flex';
            isDetailExpanded = false;
        });
    }
}

function showUnitDetail(unit) {
    const overlay = document.getElementById('unitDetailOverlay');
    const collapsed = document.getElementById('unitDetailCollapsed');
    const expanded = document.getElementById('unitDetailExpanded');

    if (!overlay) return;

    // FIXED: Initialize compass smartly
    // Prefer current heading, fallback to cache, then wait for data
    let displayHeading = null;

    if (unit && unit.heading !== null && unit.heading !== undefined && !isNaN(unit.heading)) {
        // Valid heading from unit (including 0°)
        displayHeading = unit.heading;
        lastValidHeading = unit.heading;  // Cache it
        console.log(`🧭 Compass initialized with unit heading: ${unit.heading.toFixed(1)}°`);
    } else if (lastValidHeading !== null && !isNaN(lastValidHeading)) {
        // Use previous cached heading
        displayHeading = lastValidHeading;
        console.log(`🧭 Compass initialized with cached heading: ${lastValidHeading.toFixed(1)}°`);
    } else {
        // Wait for polling data
        console.debug(`⏳ Compass waiting for heading data from polling...`);
    }

    updateCompassDisplay(displayHeading);

    const displayStatus = computeDisplayStatus(unit);

    // Update detail image icon (but don't override rotation)
    const detailImg = document.getElementById('detailUnitImage');
    if (detailImg) {
        detailImg.src = getUnitIcon(displayStatus, unit.fleet_type);
    }

    // Update detail text content
    document.getElementById('detailUnitName').textContent = unit.name;
    document.getElementById('detailUnitSpeed').textContent = `${unit.speed} Km/h`;
    document.getElementById('detailUnitSpeed').className = 'unit-detail-value speed';
    document.getElementById('detailUnitGps').textContent = unit.gps;
    document.getElementById('detailUnitGps').className = `unit-detail-value gps-${unit.gps.toLowerCase()}`;
    const satelliteEl = document.getElementById('detailUnitSatellite');
    if (satelliteEl) {
        satelliteEl.textContent = Number.isFinite(Number(unit.satellite)) ? `${unit.satellite}` : '-';
    }
    const rssiEl = document.getElementById('detailUnitRssi');
    const rsrpEl = document.getElementById('detailUnitRsrp');
    const rsrqEl = document.getElementById('detailUnitRsrq');
    const sinrEl = document.getElementById('detailUnitSinr');
    if (unit.networkQuality) {
        if (rssiEl) rssiEl.textContent = unit.networkQuality.rssi ?? '-';
        if (rsrpEl) rsrpEl.textContent = unit.networkQuality.rsrp ?? '-';
        if (rsrqEl) rsrqEl.textContent = unit.networkQuality.rsrq ?? '-';
        if (sinrEl) sinrEl.textContent = unit.networkQuality.sinr ?? '-';
    } else {
        if (rssiEl) rssiEl.textContent = '-';
        if (rsrpEl) rsrpEl.textContent = '-';
        if (rsrqEl) rsrqEl.textContent = '-';
        if (sinrEl) sinrEl.textContent = '-';
    }
    document.getElementById('detailUnitLastUpdate').textContent = unit.lastUpdate;

    // Reset to collapsed state
    collapsed.style.display = 'flex';
    expanded.classList.remove('show');
    isDetailExpanded = false;

    overlay.classList.add('visible');
}

function hideUnitDetail() {
    const overlay = document.getElementById('unitDetailOverlay');
    if (overlay) overlay.classList.remove('visible');
}

function updateDetailImageRotation(heading) {
    // FIXED: Ensure heading is a valid number
    if (typeof heading !== 'number' || isNaN(heading)) {
        console.warn(`Invalid heading for detail update: ${heading}`);
        return;
    }

    // Update truck image rotation (actual truck icon)
    const detailImg = document.getElementById('detailUnitImage');
    if (detailImg) {
        const targetRotation = headingToIconRotation(heading);
        let currentRotation = parseFloat(detailImg.dataset.currentRotation);
        if (isNaN(currentRotation)) {
            // First time
            currentRotation = targetRotation;
            detailImg.style.transition = 'none';
            setTimeout(() => detailImg.style.transition = '', 50);
        }

        let diff = (targetRotation - currentRotation) % 360;
        if (diff > 180) diff -= 360;
        if (diff < -180) diff += 360;
        const finalRotation = currentRotation + diff;

        detailImg.dataset.currentRotation = finalRotation;
        detailImg.style.transform = `rotate(${finalRotation}deg)`;
    }
}

// FIXED: Separate function just for compass number display (no SVG rotation complexity)
function updateCompassDisplay(heading) {
    const headingText = document.getElementById('miniHeadingText');
    if (!headingText) return;

    // FIXED: Use cache if heading is not valid - prevent flash
    let displayHeading = heading;

    // Check if heading is valid (not null/undefined/NaN) - 0° is VALID (north)
    if (heading === null || heading === undefined || typeof heading !== 'number' || isNaN(heading)) {
        // Use last valid heading if available
        if (lastValidHeading !== null && !isNaN(lastValidHeading)) {
            displayHeading = lastValidHeading;
            console.debug(`🧭 Using cached heading: ${displayHeading.toFixed(0)}° (new data unavailable)`);
        } else {
            // Show waiting state only if no cache available
            headingText.textContent = '--°';
            console.debug(`🧭 Compass waiting for data...`);
            return;
        }
    } else if (heading >= 0 && heading < 360) {
        // Save valid heading to cache (0-360 is valid range)
        lastValidHeading = heading;
    }

    // Display the heading (either new valid data or cached)
    headingText.textContent = `${displayHeading.toFixed(0)}°`;
}

/**
 * FIXED: Update detail card with latest unit data during auto-refresh
 * Always update truck image and compass display in real-time
 * Uses cached heading if new data unavailable to prevent flash
 * Only updates visible fields, doesn't recreate the card
 */
function updateDetailCard(unit) {
    // ALWAYS update truck image rotation and compass display in real-time
    if (unit && unit.heading !== null && unit.heading !== undefined && !isNaN(unit.heading)) {
        updateDetailImageRotation(unit.heading);
        updateCompassDisplay(unit.heading);  // Will cache this valid heading
    } else {
        // Call compass update anyway - it will use cached heading
        updateCompassDisplay(null);
    }

    // Update truck image icon based on live status (idle should be yellow)
    const detailImg = document.getElementById('detailUnitImage');
    if (detailImg && unit) {
        const displayStatus = computeDisplayStatus(unit);
        detailImg.src = getUnitIcon(displayStatus, unit.fleet_type);
    }

    // Only update visible fields if card is expanded
    if (!isDetailExpanded) return;

    // Update speed
    const speedEl = document.getElementById('detailUnitSpeed');
    if (speedEl && unit) {
        speedEl.textContent = `${unit.speed} Km/h`;
    }

    // Update GPS status
    const gpsEl = document.getElementById('detailUnitGps');
    if (gpsEl && unit) {
        gpsEl.textContent = unit.gps;
    }

    // Update satellite count
    const satelliteEl = document.getElementById('detailUnitSatellite');
    if (satelliteEl && unit) {
        satelliteEl.textContent = Number.isFinite(Number(unit.satellite)) ? `${unit.satellite}` : '-';
    }

    // Update network signal strength
    const rssiEl = document.getElementById('detailUnitRssi');
    const rsrpEl = document.getElementById('detailUnitRsrp');
    const rsrqEl = document.getElementById('detailUnitRsrq');
    const sinrEl = document.getElementById('detailUnitSinr');
    if (unit && unit.networkQuality) {
        if (rssiEl) rssiEl.textContent = unit.networkQuality.rssi ?? '-';
        if (rsrpEl) rsrpEl.textContent = unit.networkQuality.rsrp ?? '-';
        if (rsrqEl) rsrqEl.textContent = unit.networkQuality.rsrq ?? '-';
        if (sinrEl) sinrEl.textContent = unit.networkQuality.sinr ?? '-';
    } else {
        if (rssiEl) rssiEl.textContent = '-';
        if (rsrpEl) rsrpEl.textContent = '-';
        if (rsrqEl) rsrqEl.textContent = '-';
        if (sinrEl) sinrEl.textContent = '-';
    }

    // Update last update timestamp
    const lastUpdateEl = document.getElementById('detailUnitLastUpdate');
    if (lastUpdateEl && unit) {
        lastUpdateEl.textContent = unit.lastUpdate || '-';
    }

}

// Chart related code removed

// Track Replay
function initTrackReplay() {
    const trackDate = document.getElementById('trackDate');
    const submitBtn = document.getElementById('trackReplaySubmit');

    // Default to today
    const today = new Date();
    if (trackDate) trackDate.value = formatDate(today);

    if (submitBtn) {
        submitBtn.addEventListener('click', handleTrackSubmit);
    }

}

function handleTrackSubmit() {
    if (!selectedUnit) return;

    const date = document.getElementById('trackDate').value;
    if (!date) return;

    // FIXED: Open dedicated track replay page in new tab
    const replayUrl = `/tracking/replay?device_id=${selectedUnit.id}&date=${date}`;
    window.open(replayUrl, '_blank');

    // Hide track replay card on the main map
    hideTrackReplay();
}

function showTrackReplay() {
    const card = document.getElementById('trackReplayCard');
    if (card) card.classList.add('visible');
}

function hideTrackReplay() {
    const card = document.getElementById('trackReplayCard');
    if (card) card.classList.remove('visible');
}

function formatDate(date) {
    return date.toISOString().split('T')[0];
}

// TOTAL DISTANCE
function showTotalDistance() {
    const card = document.getElementById('totalDistanceCard');
    if (card) card.classList.add('visible');
}

function hideTotalDistance() {
    const card = document.getElementById('totalDistanceCard');
    if (card) card.classList.remove('visible');
}

function updateTotalDistance(km) {
    const el = document.getElementById('totalDistanceValue');
    if (el) el.textContent = `${km} Km`;
}

// LOCATE BUTTON
function locateToSite() {
    if (currentSite) {
        map.flyTo(currentSite.center, currentSite.zoom, { duration: 1 });
    } else {
        map.flyTo([-2.5, 118], 5, { duration: 1 });
    }
}

// =============================================
// DATE RANGE SELECTOR
// =============================================

function initDateRangeSelector() {
    const startInput = document.getElementById('reportDateStart');
    const endInput = document.getElementById('reportDateEnd');
    const submitBtn = document.getElementById('dateRangeSubmit');
    const resetBtn = document.getElementById('dateRangeReset');

    if (!startInput || !endInput || !submitBtn) return;

    // Default: last 3 days (to prevent performance issues with large datasets)
    const today = new Date();
    const threeDaysAgo = new Date(today);
    threeDaysAgo.setDate(threeDaysAgo.getDate() - 3);

    startInput.value = formatDate(threeDaysAgo);
    endInput.value = formatDate(today);

    submitBtn.addEventListener('click', handleDateRangeSubmit);
    if (resetBtn) {
        resetBtn.addEventListener('click', handleDateRangeReset);
    }
}

function handleDateRangeReset() {
    const startInput = document.getElementById('reportDateStart');
    const endInput = document.getElementById('reportDateEnd');
    
    // Set to today
    const today = new Date();
    const todayStr = formatDate(today);
    startInput.value = todayStr;
    endInput.value = todayStr;

    // Hide info text
    const infoDiv = document.getElementById('dateRangeInfo');
    if (infoDiv) infoDiv.style.display = 'none';

    selectedDateRange = { start: todayStr, end: todayStr };

    for (const key in TRACK_CACHE) {
        delete TRACK_CACHE[key];
    }

    // Show loading
    if (typeof Swal !== 'undefined') {
        Swal.fire({
            title: 'Sedang memproses data...',
            text: 'Mohon tunggu sebentar',
            allowOutsideClick: false,
            showConfirmButton: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });
    }

    showSiteLevelTracks().then(() => {
        if (typeof Swal !== 'undefined') Swal.close();
        
        // Update Signal Report UI if visible
        const panel = document.getElementById('signalReportPanel');
        if (panel && panel.classList.contains('visible') && currentSite) {
            if (typeof computeSignalStatsFromUnits === 'function' && typeof updateSignalReportUI === 'function') {
                const stats = computeSignalStatsFromUnits(currentSite.units);
                const pseudoUnit = {
                    name: currentSite.name || currentSite.code,
                    isSite: true,
                    unitCount: currentSite.units.length
                };
                updateSignalReportUI(stats, pseudoUnit);
            }
        }
    }).catch(err => {
        if (typeof Swal !== 'undefined') {
            Swal.fire('Error', 'Gagal memproses data: ' + err.message, 'error');
        }
    });
}

function handleDateRangeSubmit() {
    const startInput = document.getElementById('reportDateStart');
    const endInput = document.getElementById('reportDateEnd');
    const infoDiv = document.getElementById('dateRangeInfo');
    const infoText = document.getElementById('dateRangeInfoText');

    if (!startInput || !endInput) return;

    const startDate = startInput.value;
    const endDate = endInput.value;

    if (!startDate || !endDate) {
        alert('Pilih tanggal mulai dan akhir!');
        return;
    }

    if (new Date(startDate) > new Date(endDate)) {
        alert('Tanggal mulai tidak boleh lebih besar dari tanggal akhir!');
        return;
    }

    // Limit to max 3 days
    const start = new Date(startDate);
    const end = new Date(endDate);
    const diffTime = Math.abs(end - start);
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    
    if (diffDays > 3) {
        alert('Rentang waktu maksimal adalah 3 hari untuk menjaga performa sistem.');
        return;
    }

    selectedDateRange = { start: startDate, end: endDate };

    // Clear track cache so it forces a re-fetch for the new date range
    for (const key in TRACK_CACHE) {
        delete TRACK_CACHE[key];
    }

    // Show success info
    if (infoDiv && infoText) {
        const startFormatted = new Date(startDate).toLocaleDateString('id-ID', { day: '2-digit', month: 'short', year: 'numeric' });
        const endFormatted = new Date(endDate).toLocaleDateString('id-ID', { day: '2-digit', month: 'short', year: 'numeric' });
        infoText.textContent = `Report: ${startFormatted} - ${endFormatted}`;
        infoDiv.style.display = 'flex';
    }

    // Show loading popup
    if (typeof Swal !== 'undefined') {
        Swal.fire({
            title: 'Sedang memproses data...',
            text: 'Mengambil riwayat tracking untuk rentang tanggal tersebut',
            allowOutsideClick: false,
            showConfirmButton: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });
    }

    // FIXED: Show dotraces for all units of this site according to the selected date range
    showSiteLevelTracks().then(() => {
        if (typeof Swal !== 'undefined') Swal.close();
        
        // Update Signal Report UI if visible
        const panel = document.getElementById('signalReportPanel');
        if (panel && panel.classList.contains('visible') && currentSite) {
            if (typeof computeSignalStatsFromUnits === 'function' && typeof updateSignalReportUI === 'function') {
                const stats = computeSignalStatsFromUnits(currentSite.units);
                const pseudoUnit = {
                    name: currentSite.name || currentSite.code,
                    isSite: true,
                    unitCount: currentSite.units.length
                };
                updateSignalReportUI(stats, pseudoUnit);
            }
        }
    }).catch(err => {
        if (typeof Swal !== 'undefined') {
            Swal.fire('Error', 'Gagal memproses data: ' + err.message, 'error');
        }
    });

    console.log(`📊 Date range selected: ${startDate} to ${endDate} for site: ${currentSite?.code || 'none'}`);
}

async function showSiteLevelTracks() {
    clearTrack();

    if (!currentSite || !currentSite.units || currentSite.units.length === 0) return;

    try {
        const startDate = typeof selectedStartDate !== 'undefined' && selectedStartDate ? selectedStartDate : new Date().toISOString().split('T')[0];
        const endDate = typeof selectedEndDate !== 'undefined' && selectedEndDate ? selectedEndDate : startDate;

        const url = `/Tracking/Api/GetSiteTrackHistory?siteCode=${encodeURIComponent(currentSite.code)}&start_date=${encodeURIComponent(startDate)}&end_date=${encodeURIComponent(endDate)}`;
        console.log(`📡 Fetching site-wide tracking dots for site ${currentSite.code}...`);

        const response = await fetch(url);
        const data = await response.json();

        if (data && data.status === 'success') {
            const pointsData = data.points || data.raw_points || [];
            if (pointsData.length > 0) {
                trackDotLayer = buildTrackDotLayer(pointsData);
                if (trackDotLayer) {
                    trackDotLayer.addTo(map);
                }

                trackDotsVisible = true;
                setTrackDotLayerVisible(true);
                updateTrackDotToggleButton();
                applyTrackZoomVisualization();

                // Update site-wide Signal Report Today automatically
                if (typeof updateSignalReport === 'function') {
                    const normPoints = data.compact ? pointsData.map(normalizePoint) : pointsData;
                    updateSignalReport(normPoints, currentSite);
                }

                console.log(`✓ Site-wide dots rendered: ${pointsData.length} signal points displayed across all devices.`);
            }
        }
    } catch (err) {
        console.error('❌ Failed to load site track history:', err);
    }
}

function showDateRange() {
    const card = document.getElementById('dateRangeCard');
    if (card) {
        card.style.display = '';
        card.classList.add('visible');
        // Reset info display
        const infoDiv = document.getElementById('dateRangeInfo');
        if (infoDiv) infoDiv.style.display = 'none';
    }
}

function hideDateRange() {
    const card = document.getElementById('dateRangeCard');
    if (card) card.classList.remove('visible');
}

