import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import * as L from 'leaflet';
import { firstValueFrom } from 'rxjs';
import { AppConfigService } from '../service/app-config.service';
import { MainserviceService } from '../service/mainservice.service';
import {
  BOKARO_NODES,
  LADLE_STATUS,
  LOCO_STATUS,
  LadleRecord,
  LocoRecord,
  PlantNode,
  PlantStateSnapshot,
  TelemetryEvent,
  WeighbridgeState
} from './statusMeta';

const FOLLOW_ZOOM = 18;
const LOCO_W = 62;
const CAR_STEP = 20; // 26px art with 6px coupling overlap
const STAGE_H = 40;
const LABEL_H = 18;

function nodeIcon(kind: string, color: string) {
  const shape = kind === 'Weighbridge' ? '50%' : '4px';
  return L.divIcon({
    className: '',
    html: `<div class="plant-marker" style="background:${color};border-radius:${shape};box-shadow: 0 0 10px ${color}"></div>`,
    iconSize: [18, 18],
    iconAnchor: [9, 9]
  });
}

function bearingBetween([lat1, lon1]: [number, number], [lat2, lon2]: [number, number]): number {
  const toRad = (d: number) => (d * Math.PI) / 180;
  const toDeg = (r: number) => (r * 180) / Math.PI;
  const y = Math.sin(toRad(lon2 - lon1)) * Math.cos(toRad(lat2));
  const x =
    Math.cos(toRad(lat1)) * Math.sin(toRad(lat2)) -
    Math.sin(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.cos(toRad(lon2 - lon1));
  return (toDeg(Math.atan2(y, x)) + 360) % 360;
}

@Component({
  selector: 'app-map',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './map.component.html',
  styleUrl: './map.component.css'
})
export class MapComponent implements OnInit, OnDestroy {
  readonly Math = Math;
  map!: L.Map;

  // Base Layers
  baseLayers: { [name: string]: L.TileLayer } = {};
  currentBaseLayerName: string = 'Bright Satellite';

  // State
  state: PlantStateSnapshot | null = null;
  connected: boolean = true;
  isApiData: boolean = false;
  trackedLocoId: string | null = null;
  activeTab: 'Locos' | 'Ladles' | 'Weighbridge' | 'Events' = 'Locos';

  // Leaflet references
  private locoMarkers: { [locoId: string]: L.Marker } = {};
  private nodeMarkers: L.Marker[] = [];
  private trackLayerGroup = L.layerGroup();
  private hasFlownLocoId: string | null = null;
  private prevPositions: { [locoId: string]: [number, number] } = {};

  // Internal timer & simulation
  private intervalId: any;
  private simStep = 0;
  private isPollingActive = false;

  // Pre-calculated route segments for Bokaro Yard Simulation
  private bokaroRoutes: {
    [locoId: string]: {
      path: [number, number][];
      index: number;
      forward: boolean;
      ladles: LadleRecord[];
      status: string;
      dest: string;
    };
  } = {};

  private bokaroLadles: LadleRecord[] = [];
  private bokaroWeighbridge: WeighbridgeState = { busy: false, occupiedBy: null, queue: [] };
  private bokaroEvents: TelemetryEvent[] = [];

  constructor(
    private cdr: ChangeDetectorRef,
    private http: HttpClient,
    private configService: AppConfigService,
    private mainService: MainserviceService
  ) {}

  ngOnInit() {
    this.initMap();
    this.loadTracks();
    this.loadStaticLocations();
    this.initBokaroRoutes();
    this.startLivePolling();
  }

  ngOnDestroy() {
    if (this.intervalId) {
      clearInterval(this.intervalId);
    }
    if (this.map) {
      this.map.remove();
    }
  }

  // ─── 1. MAP INITIALIZATION & SATELLITE BASES (BOKARO YARD) ───
  private initMap() {
    const center: [number, number] = [23.633, 86.298];
    const southWest = L.latLng(23.625, 86.285);
    const northEast = L.latLng(23.640, 86.310);
    const bounds = L.latLngBounds(southWest, northEast);

    // 1. High-Clarity Satellite with Brightness & Contrast Filter
    const brightSatellite = L.tileLayer(
      'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
      {
        attribution: 'Tiles &copy; Esri, Maxar | Bokaro Industrial Rail Telemetry',
        maxZoom: 20,
        className: 'bright-satellite-layer'
      }
    );

    // 2. Google Satellite / Hybrid
    const googleSatellite = L.tileLayer(
      'https://mt1.google.com/vt/lyrs=y&x={x}&y={y}&z={z}',
      {
        attribution: '&copy; Google Maps Satellite',
        maxZoom: 20
      }
    );

    // 3. Natural Esri Satellite
    const naturalSatellite = L.tileLayer(
      'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
      {
        attribution: 'Tiles &copy; Esri, Maxar',
        maxZoom: 20
      }
    );

    // 4. Carto Voyager (Bright Vector)
    const cartoVoyager = L.tileLayer(
      'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png',
      {
        attribution: '&copy; OpenStreetMap contributors &copy; CARTO',
        maxZoom: 20
      }
    );

    // 5. Dark Tactical HUD
    const darkTactical = L.tileLayer(
      'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
      {
        attribution: '&copy; CARTO Dark Telemetry',
        maxZoom: 20
      }
    );

    // 6. OpenStreetMap Default
    const defaultMap = L.tileLayer(
      'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
      {
        attribution: '&copy; OpenStreetMap contributors',
        maxZoom: 19
      }
    );

    this.baseLayers = {
      'Bright Satellite': brightSatellite,
      'Google Satellite': googleSatellite,
      'Natural Satellite': naturalSatellite,
      'Voyager (Bright)': cartoVoyager,
      'Dark Tactical': darkTactical,
      'Light Map': defaultMap
    };

    this.map = L.map('map', {
      center,
      zoom: 16.5,
      minZoom: 14.5,
      maxZoom: 20,
      maxBounds: bounds,
      maxBoundsViscosity: 0.8,
      layers: [brightSatellite],
      zoomControl: false
    });

    L.control.zoom({ position: 'topleft' }).addTo(this.map);
    L.control.layers(this.baseLayers, {}, { position: 'topright' }).addTo(this.map);

    this.trackLayerGroup.addTo(this.map);

    // Auto-disengage camera follow controller on manual user map interaction
    this.map.on('dragstart', () => {
      if (this.trackedLocoId) {
        this.selectLoco(null);
      }
    });

    this.map.on('wheel', () => {
      if (this.trackedLocoId) {
        this.selectLoco(null);
      }
    });

    // Invalidate map size after initial paint
    setTimeout(() => {
      if (this.map) {
        this.map.invalidateSize();
      }
    }, 200);
  }

  switchBaseLayer(layerName: string) {
    if (!this.baseLayers[layerName]) return;
    Object.values(this.baseLayers).forEach((layer) => {
      if (this.map.hasLayer(layer)) {
        this.map.removeLayer(layer);
      }
    });
    this.baseLayers[layerName].addTo(this.map);
    this.currentBaseLayerName = layerName;
    this.cdr.detectChanges();
  }

  // ─── 2. PRECISE BOKARO YARD TRACK GEOMETRY ───
  private getSmoothCurve(
    start: [number, number],
    ctrl: [number, number],
    end: [number, number],
    steps = 30
  ): [number, number][] {
    const pts: [number, number][] = [];
    for (let i = 0; i <= steps; i++) {
      const t = i / steps;
      const lat = (1 - t) ** 2 * start[0] + 2 * (1 - t) * t * ctrl[0] + t ** 2 * end[0];
      const lng = (1 - t) ** 2 * start[1] + 2 * (1 - t) * t * ctrl[1] + t ** 2 * end[1];
      pts.push([lat, lng]);
    }
    return pts;
  }

  loadTracks() {
    this.trackLayerGroup.clearLayers();

    // Exact Bokaro Coordinates
    const coords = {
      BF1: [23.630511, 86.290917] as [number, number],
      BF2: [23.630522, 86.292500] as [number, number],
      BF3: [23.630600, 86.296111] as [number, number],
      WB:  [23.631639, 86.298406] as [number, number],
      DIP: [23.634329, 86.299567] as [number, number],
      PCM: [23.634310, 86.299506] as [number, number],
      LRS: [23.634917, 86.300306] as [number, number],
      SMS: [23.635417, 86.301056] as [number, number],
      DIP_End: [23.635641, 86.299413] as [number, number],
      PCM_End: [23.635641, 86.299313] as [number, number]
    };

    const drawTrack = (latlngs: [number, number][]) => {
      // 1. Dark robust base (Sleepers / Ballast)
      L.polyline(latlngs, {
        color: '#1e293b',
        weight: 5,
        opacity: 0.95,
        lineCap: 'round',
        lineJoin: 'round'
      }).addTo(this.trackLayerGroup);

      // 2. Light dashed rail top (Steel Rails)
      L.polyline(latlngs, {
        color: '#f8fafc',
        weight: 2.2,
        dashArray: '5, 7',
        opacity: 0.9
      }).addTo(this.trackLayerGroup);
    };

    const RAIL_OFFSET = 0.000035;
    const railTop = (pt: [number, number]): [number, number] => [pt[0] + RAIL_OFFSET, pt[1]];
    const railBottom = (pt: [number, number]): [number, number] => [pt[0] - RAIL_OFFSET, pt[1]];

    const drawParallelTrack = (from: [number, number], to: [number, number]) => {
      drawTrack([railTop(from), railTop(to)]);
      drawTrack([railBottom(from), railBottom(to)]);
    };

    // 1. Straight Parallel Tracks: BF1 -> BF2 -> BF3
    drawParallelTrack(coords.BF1, coords.BF2);
    drawParallelTrack(coords.BF2, coords.BF3);

    // Crossover Diamonds at BF1, BF2, BF3
    const drawCrossover = (inA: [number, number], inB: [number, number], mid: [number, number]) => {
      drawTrack([inA, mid, inB]);
      drawTrack([inB, mid, inA]);
    };
    drawCrossover(railTop(coords.BF1), railBottom(coords.BF1), coords.BF1);
    drawCrossover(railTop(coords.BF2), railBottom(coords.BF2), coords.BF2);
    drawCrossover(railTop(coords.BF3), railBottom(coords.BF3), coords.BF3);

    // 2. Curved Section: BF3 -> WB
    const wbCtrl1: [number, number] = [23.630600, 86.297900];
    const wbCtrl2: [number, number] = [23.630550, 86.297950];
    const wbCurveTop = this.getSmoothCurve(railTop(coords.BF3), wbCtrl1, railTop(coords.WB));
    const wbCurveBottom = this.getSmoothCurve(railBottom(coords.BF3), wbCtrl2, railBottom(coords.WB));
    drawTrack(wbCurveTop);
    drawTrack(wbCurveBottom);

    // 3. Branches from WB
    const smsCurve = this.getSmoothCurve(coords.WB, [23.633250, 86.299600], coords.SMS);
    drawTrack(smsCurve);

    const lrsCurve = this.getSmoothCurve([23.633888, 86.300027], [23.633971, 86.300155], coords.LRS);
    drawTrack(lrsCurve);

    const pcmCurve = this.getSmoothCurve([23.632768, 86.299200], [23.633500, 86.299600], coords.PCM);
    drawTrack(pcmCurve);

    const dipCurve = this.getSmoothCurve(coords.WB, [23.633650, 86.299990], coords.DIP);
    drawTrack(dipCurve);

    const dipExt = this.getSmoothCurve(coords.DIP, [23.635542, 86.299480], coords.DIP_End);
    drawTrack(dipExt);

    const pcmExt = this.getSmoothCurve(coords.PCM, [23.635542, 86.299380], coords.PCM_End);
    drawTrack(pcmExt);
  }

  // ─── 3. STATIC PLANT LOCATIONS (API-FIRST WITH SIMULATION FALLBACK) ───
  async loadStaticLocations() {
    const apiBase = (this.configService.apiUrl || '').replace(/\/+$/, '');
    const endpoints = [
      `${apiBase}/loco/locations`,
      `${apiBase}/api/map/static-locations`,
      `${apiBase}/static-locations`,
      `http://localhost:5173/api/map/static-locations`
    ];

    let loadedNodes: Record<string, PlantNode> | null = null;

    if (apiBase) {
      for (const url of endpoints) {
        try {
          const res: any = await firstValueFrom(this.http.get(url));
          const list = Array.isArray(res) ? res : res?.data;
          if (Array.isArray(list) && list.length > 0) {
            loadedNodes = {};
            list.forEach((loc: any, idx: number) => {
              const id = loc.id || loc.code || loc.name || `NODE-${idx + 1}`;
              const lat = Number(loc.lat ?? loc.latitude ?? loc.pos?.[0]);
              const lng = Number(loc.lng ?? loc.longitude ?? loc.pos?.[1]);
              if (!isNaN(lat) && !isNaN(lng)) {
                loadedNodes![id] = {
                  id,
                  name: loc.name || id,
                  type: loc.type || 'junction',
                  kind: loc.kind || 'Plant Node',
                  color: loc.color || '#3b82f6',
                  pos: [lat, lng],
                  description: loc.description || ''
                };
              }
            });
            if (Object.keys(loadedNodes).length > 0) {
              console.log(`[Trackmap] Loaded ${Object.keys(loadedNodes).length} static plant locations from API: ${url}`);
              break;
            }
          }
        } catch {
          // Continue to next endpoint or fallback
        }
      }
    }

    // Fallback to predefined BOKARO_NODES if API returned no data
    const nodesToRender = loadedNodes && Object.keys(loadedNodes).length > 0 ? loadedNodes : BOKARO_NODES;
    this.renderStaticLocationMarkers(nodesToRender);
  }

  private renderStaticLocationMarkers(nodes: Record<string, PlantNode>) {
    this.nodeMarkers.forEach((m) => this.map.removeLayer(m));
    this.nodeMarkers = [];

    Object.values(nodes).forEach((n) => {
      const marker = L.marker(n.pos, {
        icon: nodeIcon(n.kind, n.color),
        zIndexOffset: 600
      }).addTo(this.map);

      marker.bindTooltip(`<b>${n.name}</b>`, {
        direction: 'top',
        offset: [0, -8],
        permanent: true,
        className: 'plant-node-tooltip'
      });

      const popupHtml = `
        <div class="node-3d-popup">
          <div class="popup-title" style="color: ${n.color}; font-weight: 800; font-size: 14px;">
            ${n.name}
          </div>
          <div style="font-size: 11px; color: #94a3b8; margin: 4px 0;">${n.kind}</div>
          <div style="font-size: 11.5px; color: #cbd5e1;">${n.description || 'Active plant operational unit'}</div>
          <div style="margin-top: 8px; font-size: 10.5px; color: #64748b;">
            📍 Lat: ${n.pos[0].toFixed(5)}, Lon: ${n.pos[1].toFixed(5)}
          </div>
        </div>
      `;
      marker.bindPopup(popupHtml, { className: 'dark-glass-popup' });
      this.nodeMarkers.push(marker);
    });
  }

  // ─── 4. 3D COUPLED TRAIN ICON FACTORY ───
  private createLocoIcon(
    status: string,
    bearing: number,
    label: string,
    ladlesForLoco: LadleRecord[],
    tracked: boolean
  ): L.DivIcon {
    const meta = LOCO_STATUS[status] || LOCO_STATUS['IDLE'];
    const color = meta.color || '#6b7280';
    const rotation = bearing - 90;
    const n = ladlesForLoco.length;
    const totalWidth = n * CAR_STEP + LOCO_W;
    const locoCenterX = totalWidth - LOCO_W / 2;
    const pivot = `${locoCenterX}px 50%`;

    const carsHtml = ladlesForLoco
      .map((ladle) => {
        const isHot = ladle.netKg > 0;
        const img = isHot ? 'assets/icons/ladle-full.png' : 'assets/icons/ladle-empty.png';
        const glow = isHot ? 'ladle-glow' : '';
        const tooltip = isHot
          ? `${(ladle.netKg / 1000).toFixed(1)}t Molten (${ladle.temperatureC || 1440}°C)`
          : `Tare: ${(ladle.tareKg / 1000).toFixed(1)}t`;
        return `
          <div class="ladle-wrapper" title="${ladle.id}: ${tooltip}">
            <img class="ladle-img ${glow}" src="${img}" alt="ladle" draggable="false" />
            ${isHot ? `<div class="molten-heat-shimmer"></div>` : ''}
          </div>
        `;
      })
      .join('');

    const html = `
      <div class="loco-marker-wrap" style="width:${totalWidth}px">
        <div class="train-stage" style="width:${totalWidth}px; height:${STAGE_H}px">
          ${tracked ? `<div class="track-ring" style="left:${locoCenterX}px"></div>` : ''}
          <div class="ground-glow" style="left:${locoCenterX}px; background: radial-gradient(ellipse, ${color}88 0%, ${color}00 70%)"></div>
          <div class="train-unit" style="transform: rotate(${rotation}deg); transform-origin: ${pivot}">
            ${carsHtml}
            <img class="loco-img" src="assets/icons/loco.png" alt="loco" draggable="false" />
          </div>
        </div>
        <div class="loco-label" style="border-color:${color}; left:${locoCenterX}px">
          <span class="dot" style="background:${color}"></span> ${label}
        </div>
      </div>
    `;

    return L.divIcon({
      className: `loco-anim ${tracked ? 'loco-tracked' : ''}`,
      html,
      iconSize: [totalWidth, STAGE_H + LABEL_H],
      iconAnchor: [locoCenterX, STAGE_H / 2]
    });
  }

  // ─── 5. SIMULATION DATA GENERATION (BOKARO YARD ROUTES) ───
  private initBokaroRoutes() {
    const c = {
      BF1: [23.630511, 86.290917] as [number, number],
      BF2: [23.630522, 86.292500] as [number, number],
      BF3: [23.630600, 86.296111] as [number, number],
      WB:  [23.631639, 86.298406] as [number, number],
      SMS: [23.635417, 86.301056] as [number, number],
      DIP: [23.634329, 86.299567] as [number, number],
      PCM: [23.634310, 86.299506] as [number, number],
      LRS: [23.634917, 86.300306] as [number, number]
    };

    // Route 1: BF1 -> BF2 -> BF3 -> WB -> SMS (2 Heavy Molten Metal Ladles)
    const p1: [number, number][] = [
      c.BF1, c.BF2, c.BF3,
      ...this.getSmoothCurve(c.BF3, [23.630600, 86.297900], c.WB, 18),
      ...this.getSmoothCurve(c.WB, [23.633250, 86.299600], c.SMS, 25)
    ];

    // Route 2: BF2 -> BF3 -> WB -> DIP -> PCM (1 Heavy Molten Metal Ladle)
    const p2: [number, number][] = [
      c.BF2, c.BF3,
      ...this.getSmoothCurve(c.BF3, [23.630600, 86.297900], c.WB, 15),
      ...this.getSmoothCurve(c.WB, [23.633650, 86.299990], c.DIP, 18),
      ...this.getSmoothCurve(c.DIP, [23.634310, 86.299506], c.PCM, 12)
    ];

    // Route 3: LRS -> WB -> BF3 (Empty Ladle Return Consist)
    const p3: [number, number][] = [
      c.LRS,
      ...this.getSmoothCurve([23.633888, 86.300027], [23.633971, 86.300155], c.WB, 16).reverse(),
      ...this.getSmoothCurve(c.BF3, [23.630600, 86.297900], c.WB, 16).reverse(),
      c.BF3
    ];

    // Route 4: SMS -> WB -> BF1 (Returning Empty Consist)
    const p4: [number, number][] = [
      c.SMS,
      ...this.getSmoothCurve(c.WB, [23.633250, 86.299600], c.SMS, 20).reverse(),
      c.WB,
      ...this.getSmoothCurve(c.BF3, [23.630600, 86.297900], c.WB, 15).reverse(),
      c.BF3, c.BF2, c.BF1
    ];

    this.bokaroLadles = [
      { id: 'L-101', status: 'FULL_IN_TRANSIT', locationNode: 'BF1', tareKg: 42000, netKg: 78500, assignedLoco: 'LOCO-101', temperatureC: 1445 },
      { id: 'L-102', status: 'FULL_IN_TRANSIT', locationNode: 'BF1', tareKg: 41500, netKg: 74200, assignedLoco: 'LOCO-101', temperatureC: 1438 },
      { id: 'L-204', status: 'FULL_IN_TRANSIT', locationNode: 'BF2', tareKg: 43000, netKg: 71000, assignedLoco: 'LOCO-102', temperatureC: 1452 },
      { id: 'L-301', status: 'EMPTY_RETURNING', locationNode: 'LRS', tareKg: 42000, netKg: 0, assignedLoco: 'LOCO-103', temperatureC: 310 },
      { id: 'L-408', status: 'EMPTY_RETURNING', locationNode: 'SMS', tareKg: 42500, netKg: 0, assignedLoco: 'LOCO-104', temperatureC: 280 }
    ];

    this.bokaroRoutes = {
      'LOCO-101': {
        path: p1,
        index: 0,
        forward: true,
        ladles: [this.bokaroLadles[0], this.bokaroLadles[1]],
        status: 'TO_WEIGHBRIDGE',
        dest: 'SMS Plant'
      },
      'LOCO-102': {
        path: p2,
        index: 10,
        forward: true,
        ladles: [this.bokaroLadles[2]],
        status: 'TO_WEIGHBRIDGE',
        dest: 'DIP Pretreatment'
      },
      'LOCO-103': {
        path: p3,
        index: 4,
        forward: true,
        ladles: [this.bokaroLadles[3]],
        status: 'RETURNING',
        dest: 'BF-3 Mega Furnace'
      },
      'LOCO-104': {
        path: p4,
        index: 18,
        forward: true,
        ladles: [this.bokaroLadles[4]],
        status: 'RETURNING',
        dest: 'BF-1 Taphole'
      }
    };

    this.bokaroEvents = [
      { type: 'DEPARTURE', message: 'LOCO-101 departed BF1 with 2 hot metal ladles (L-101, L-102), heading to Weighbridge.', created_at: new Date().toISOString() },
      { type: 'WEIGHBRIDGE', message: 'LOCO-102 in-motion telemetry weighing recorded (Total Gross: 114,000 kg).', created_at: new Date(Date.now() - 40000).toISOString() },
      { type: 'ARRIVAL', message: 'LOCO-104 arrived at SMS BOF converters for discharge.', created_at: new Date(Date.now() - 85000).toISOString() }
    ];
  }

  // ─── 6. LIVE POLLING (API-FIRST WITH SIMULATION FALLBACK) ───
  private startLivePolling() {
    this.pollLiveLocos();
    this.intervalId = setInterval(() => {
      this.pollLiveLocos();
    }, 1000);
  }

  private async pollLiveLocos() {
    if (this.isPollingActive) return;
    this.isPollingActive = true;

    const apiBase = (this.configService.apiUrl || '').replace(/\/+$/, '');
    const endpoints = [
      `${apiBase}/loco/simulate`,
      `${apiBase}/api/map/simulate`,
      `${apiBase}/api/Tracking/simulate`,
      `http://localhost:5173/api/map/simulate`
    ];

    let apiData: any = null;

    if (apiBase) {
      for (const url of endpoints) {
        try {
          const res: any = await firstValueFrom(this.http.get(url));
          const list = Array.isArray(res) ? res : res?.data;
          if (Array.isArray(list) && list.length > 0) {
            apiData = list;
            break;
          }
        } catch {
          // Try next endpoint
        }
      }
    }

    if (apiData && apiData.length > 0) {
      this.connected = true;
      this.isApiData = true;
      this.processApiLocoData(apiData);
    } else {
      // Fallback seamlessly to Bokaro Yard simulation
      this.connected = true;
      this.isApiData = false;
      this.advanceBokaroInternalStep();
    }

    this.isPollingActive = false;
  }

  private processApiLocoData(rawList: any[]) {
    const locos: LocoRecord[] = rawList.map((item: any, idx: number) => {
      const locoIdNum = item.locoId || item.id || (101 + idx);
      const locoIdStr = `LOCO-${locoIdNum}`;

      const lat = item.latitude !== undefined ? Number(item.latitude) : Number(item.lat);
      const lon = item.longitude !== undefined ? Number(item.longitude) : (item.lng !== undefined ? Number(item.lng) : Number(item.lon));
      const currentPos: [number, number] = [lat, lon];

      const prevPos = this.prevPositions[locoIdStr] || currentPos;
      const bearing = item.bearing !== undefined ? Number(item.bearing) : bearingBetween(prevPos, currentPos);
      this.prevPositions[locoIdStr] = currentPos;

      const isHot = item.ladle === 'Hot Metal' || (item.netKg && item.netKg > 0);
      const ladles: LadleRecord[] = item.ladles
        ? item.ladles.map((l: any, lIdx: number) => ({
            id: l.number || l.id || `L-${locoIdNum}-${lIdx + 1}`,
            status: l.temp > 1000 || l.netKg > 0 ? 'FULL_IN_TRANSIT' : 'EMPTY_RETURNING',
            locationNode: item.destination || 'Track',
            tareKg: l.tareKg || 42000,
            netKg: l.netKg || (l.temp > 1000 ? 72000 : 0),
            temperatureC: l.temp || (isHot ? 1440 : 300)
          }))
        : isHot
        ? [
            {
              id: `L-${locoIdNum}`,
              status: 'FULL_IN_TRANSIT',
              locationNode: item.destination || 'SMS',
              tareKg: 42000,
              netKg: item.netKg || 74500,
              temperatureC: 1445
            }
          ]
        : [
            {
              id: `L-${locoIdNum}`,
              status: 'EMPTY_RETURNING',
              locationNode: 'BF1',
              tareKg: 42000,
              netKg: 0,
              temperatureC: 300
            }
          ];

      return {
        id: locoIdStr,
        locoId: locoIdNum,
        pos: currentPos,
        latitude: lat,
        longitude: lon,
        bearing,
        speedKmh: item.speedKmh || item.speed || 14,
        speed: item.speed || item.speedKmh || 14,
        status: isHot ? 'HOT_METAL_TRANSIT' : item.status || 'IN_TRANSIT',
        destination: item.destination || 'SMS BOF Converter',
        ladleIds: ladles.map((l) => l.id),
        ladles
      };
    });

    const allLadles = locos.flatMap((l) => l.ladles || []);
    const snapshot: PlantStateSnapshot = {
      timestamp: new Date().toISOString(),
      nodes: BOKARO_NODES,
      railSegments: {},
      weighbridge: {
        busy: locos.some((l) => l.status === 'WEIGHING'),
        occupiedBy: locos.find((l) => l.status === 'WEIGHING')?.id || null,
        queue: []
      },
      locos,
      ladles: allLadles.length > 0 ? allLadles : this.bokaroLadles,
      events: this.bokaroEvents
    };

    this.handleSnapshotUpdate(snapshot);
  }

  private advanceBokaroInternalStep() {
    this.simStep++;

    const locos: LocoRecord[] = Object.entries(this.bokaroRoutes).map(([id, route]) => {
      const totalPoints = route.path.length;

      // Advance along route path
      if (route.forward) {
        route.index = (route.index + 1) % totalPoints;
        if (route.index === totalPoints - 1) route.forward = false;
      } else {
        route.index = (route.index - 1 + totalPoints) % totalPoints;
        if (route.index === 0) route.forward = true;
      }

      const currentPos = route.path[route.index];
      const nextPos = route.forward
        ? route.path[Math.min(route.index + 1, totalPoints - 1)]
        : route.path[Math.max(route.index - 1, 0)];

      const bearing = bearingBetween(currentPos, nextPos);
      const isHot = route.ladles.some((l) => l.netKg > 0);
      const speed = isHot ? 13 : 18;

      return {
        id,
        locoId: parseInt(id.replace('LOCO-', ''), 10) || 101,
        pos: currentPos,
        latitude: currentPos[0],
        longitude: currentPos[1],
        bearing,
        speedKmh: speed,
        speed,
        status: isHot ? 'HOT_METAL_TRANSIT' : 'IN_TRANSIT',
        destination: route.dest,
        ladleIds: route.ladles.map((l) => l.id),
        ladles: route.ladles
      };
    });

    const snapshot: PlantStateSnapshot = {
      timestamp: new Date().toISOString(),
      nodes: BOKARO_NODES,
      railSegments: {},
      weighbridge: this.bokaroWeighbridge,
      locos,
      ladles: this.bokaroLadles,
      events: this.bokaroEvents
    };

    this.handleSnapshotUpdate(snapshot);
  }

  private handleSnapshotUpdate(snapshot: PlantStateSnapshot) {
    this.state = snapshot;
    this.renderLocos(snapshot.locos, snapshot.ladles);
    this.handleFollowController(snapshot.locos);
    this.cdr.detectChanges();
  }

  private renderLocos(locos: LocoRecord[], ladles: LadleRecord[]) {
    const ladleById = Object.fromEntries(ladles.map((l) => [l.id, l]));

    locos.forEach((loco) => {
      const isTracked = loco.id === this.trackedLocoId;
      const consist = loco.ladleIds.map((id) => ladleById[id]).filter(Boolean);
      const icon = this.createLocoIcon(
        loco.status,
        loco.bearing,
        loco.id.replace('LOCO-', 'L'),
        consist,
        isTracked
      );

      if (!this.locoMarkers[loco.id]) {
        const marker = L.marker(loco.pos, {
          icon,
          zIndexOffset: isTracked ? 3000 : 1500
        }).addTo(this.map);

        marker.on('click', () => {
          this.selectLoco(this.trackedLocoId === loco.id ? null : loco.id);
        });

        this.attachPopup(marker, loco, consist);
        this.locoMarkers[loco.id] = marker;
      } else {
        const marker = this.locoMarkers[loco.id];
        marker.setLatLng(loco.pos);
        marker.setIcon(icon);
        marker.setZIndexOffset(isTracked ? 3000 : 1500);
        this.attachPopup(marker, loco, consist);
      }
    });
  }

  private attachPopup(marker: L.Marker, loco: LocoRecord, consist: LadleRecord[]) {
    const totalNet = consist.reduce((sum, l) => sum + l.netKg, 0);
    const meta = LOCO_STATUS[loco.status] || LOCO_STATUS['IDLE'];

    const popupHtml = `
      <div class="loco-3d-popup">
        <div style="display: flex; justify-content: space-between; align-items: center; border-left: 3px solid ${meta.color}; padding-left: 8px;">
          <strong style="font-size: 14px; color: #fff;">${loco.id}</strong>
          <span style="font-size: 11px; font-weight: 700; color: ${meta.color}; background: ${meta.badgeBg}; padding: 2px 6px; border-radius: 4px;">
            ${meta.label}
          </span>
        </div>
        <div style="margin-top: 8px; font-size: 12px; display: grid; grid-template-columns: 1fr 1fr; gap: 6px;">
          <div><span style="color: #94a3b8;">Speed:</span> <strong>${loco.speedKmh} km/h</strong></div>
          <div><span style="color: #94a3b8;">Bearing:</span> <strong>${Math.round(loco.bearing)}°</strong></div>
        </div>
        ${loco.destination ? `<div style="margin-top: 6px; font-size: 12px; color: #38bdf8;">➔ Destination: <strong>${loco.destination}</strong></div>` : ''}
        <div style="margin-top: 8px; border-top: 1px solid rgba(255,255,255,0.1); padding-top: 6px; font-size: 11.5px;">
          <div style="color: #94a3b8;">Coupled Consist (${consist.length} Ladle${consist.length > 1 ? 's' : ''}):</div>
          <div style="font-weight: 600; color: #fff; margin-top: 2px;">${consist.length ? consist.map((l) => `${l.id} (${l.netKg > 0 ? (l.netKg / 1000).toFixed(1) + 't net' : 'Empty'})`).join(', ') : 'No ladles'}</div>
          ${totalNet > 0 ? `<div style="color: #fb923c; font-weight: 700; margin-top: 4px;">🔥 Total Hot Metal: ${(totalNet / 1000).toFixed(1)} Tons</div>` : ''}
        </div>
      </div>
    `;

    marker.bindPopup(popupHtml, { className: 'dark-glass-popup' });
  }

  // ─── 7. FOLLOW CAMERA CONTROLLER ───
  private handleFollowController(locos: LocoRecord[]) {
    if (!this.trackedLocoId) {
      this.hasFlownLocoId = null;
      return;
    }

    const tracked = locos.find((l) => l.id === this.trackedLocoId);
    if (!tracked) return;

    if (this.hasFlownLocoId !== tracked.id) {
      this.hasFlownLocoId = tracked.id;
      this.map.flyTo(tracked.pos, Math.max(this.map.getZoom(), FOLLOW_ZOOM), { duration: 0.8 });
    } else {
      this.map.panTo(tracked.pos, { animate: true, duration: 0.45, easeLinearity: 1 });
    }
  }

  selectLoco(id: string | null) {
    this.trackedLocoId = id;
    if (!id) {
      this.hasFlownLocoId = null;
    } else {
      const loco = this.state?.locos.find((l) => l.id === id);
      if (loco) {
        this.map.flyTo(loco.pos, FOLLOW_ZOOM, { duration: 0.8 });
        if (this.locoMarkers[loco.id]) {
          this.locoMarkers[loco.id].openPopup();
        }
      }
    }
    this.cdr.detectChanges();
  }

  getTrackedLoco(): LocoRecord | undefined {
    return this.state?.locos.find((l) => l.id === this.trackedLocoId);
  }

  getLocoStatus(status: string) {
    return LOCO_STATUS[status] || LOCO_STATUS['IDLE'];
  }

  getLadleStatus(status: string) {
    return LADLE_STATUS[status] || LADLE_STATUS['EMPTY'];
  }

  // StatBar Calculations
  getActiveLocosCount(): number {
    return this.state?.locos.filter((l) => l.status !== 'IDLE').length || 0;
  }

  getLoadedLadlesCount(): number {
    return this.state?.ladles.filter((l) => ['FULL_IN_TRANSIT', 'FULL_TO_DEST', 'HOT_METAL_TRANSIT'].includes(l.status)).length || 0;
  }

  getAtWeighbridgeCount(): number {
    return this.state?.locos.filter((l) => ['WEIGHING', 'QUEUED_AT_WEIGHBRIDGE'].includes(l.status)).length || 0;
  }

  getWeighbridgeOccupant(): LocoRecord | undefined {
    return this.state?.locos.find((l) => l.id === this.state?.weighbridge.occupiedBy);
  }
}

// Aliases for compatibility
export { MapComponent as TrackmapComponent, MapComponent as Trackmap };